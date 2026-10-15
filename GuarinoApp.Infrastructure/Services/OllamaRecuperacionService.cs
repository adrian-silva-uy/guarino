using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;

namespace GuarinoApp.Infrastructure.Services;

/// <summary>
/// Auto-reparación silenciosa de la IA local (Ollama) + informe al administrador.
///
/// Los dos fallos reales que se dieron en pruebas:
///   (a) "colgado": el proceso sigue respondiendo el listado de modelos pero la
///       GENERACIÓN devuelve HTTP 5xx — OllamaStartupService no lo ve (su
///       chequeo solo consulta si el API está arriba) y la IA local queda inútil
///       hasta reiniciar el proceso;
///   (b) "caído": el proceso dejó de responder (conexión rechazada).
///
/// Qué hace: cuando OllamaAIService reporta un fallo de generación
/// (OllamaConnectionState.UltimoFalloEn) o el monitoreo marca el API caído
/// (IsConnected=false), verifica el estado con una generación de prueba real y,
/// si el fallo se confirma, reinicia/inicia el proceso de Ollama, vuelve a
/// verificar y manda UN correo a Notificaciones:EmailAdmin con lo ocurrido y
/// cómo se solucionó (o qué falta hacer si no pudo).
///
/// Los usuarios no lo notan: mientras tanto, la IA web y los planes
/// predefinidos siguen atendiendo (la carrera local → web → determinista ya
/// existe) y, al repararse, la ruta local vuelve a estar disponible sola.
///
/// Límites y política de avisos:
///   · UN correo por incidente a Notificaciones:EmailAdmin — "reparada
///     automáticamente" al recuperarse o "requiere intervención" si no se pudo;
///     no se manda un correo por cada intento fallido;
///   · solo actúa si Ollama:Url es local (localhost) — en otra máquina no hay
///     qué reiniciar desde acá y el reporte de operación ya cubre el aviso;
///   · máximo Ollama:Sanacion:MaxReiniciosPorHora reinicios por hora: si no se
///     recupera se avisa una sola vez y se sigue reintentando en silencio cada
///     hora hasta que vuelva a funcionar (o alguien lo arregle a mano);
///   · NO reinicia por errores 4xx (modelo en descarga, configuración): eso lo
///     resuelve el arranque existente, un reinicio no arreglaría nada.
///
/// Además limpia procesos de ejecución ("llama-server") residuales: un reinicio
/// brusco del servicio puede dejar uno huérfano (proceso padre muerto) que
/// responde HTTP 500 a TODA generación aunque el API siga arriba — el fallo
/// real que se dio en pruebas y la razón por la que reiniciar solo ollama.exe
/// no alcanzaba.
/// </summary>
public class OllamaRecuperacionService : BackgroundService
    {
    private static readonly TimeSpan VentanaDeReinicios = TimeSpan.FromHours(1);

    private readonly IHttpClientFactory _httpFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<OllamaOptions> _optionsMonitor;
    private readonly IOptionsMonitor<NotificacionesOptions> _notificaciones;
    private readonly OllamaConnectionState _connectionState;
    private readonly ILogger<OllamaRecuperacionService> _logger;

    // Último fallo de generación ya evaluado (para no repetir el mismo 500 en
    // cada ciclo; un fallo nuevo vuelve a sellar UltimoFalloEn y se reevalúa).
    private DateTime? _falloProcesado;

    // Intentos de reparación dentro de la ventana deslizante de una hora.
    private readonly List<DateTime> _intentos = new();
    private bool _avisoDeIntervencionEnviado;

    // Incidente activo: una reparación no alcanzó y se sigue reintentando en
    // cada ciclo (verificando) hasta que se recupere o se avise una vez.
    private bool _reparacionFallida;
    private string? _ultimoFalloReparacion;

    // Evita solapar dos reparaciones si una tarda más que el intervalo.
    private readonly SemaphoreSlim _semaforo = new(1, 1);

    public OllamaRecuperacionService(
        IHttpClientFactory httpFactory,
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<OllamaOptions> optionsMonitor,
        IOptionsMonitor<NotificacionesOptions> notificaciones,
        OllamaConnectionState connectionState,
        ILogger<OllamaRecuperacionService> logger)
        {
        _httpFactory = httpFactory;
        _scopeFactory = scopeFactory;
        _optionsMonitor = optionsMonitor;
        _notificaciones = notificaciones;
        _connectionState = connectionState;
        _logger = logger;
        }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
        // Dejamos que OllamaStartupService haga su chequeo inicial de arranque
        // (reintentos por ~2 min por si el servidor recién arrancó) antes de
        // pelear por reparar.
        try { await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken); }
        catch (TaskCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
            {
            var opciones = _optionsMonitor.CurrentValue;
            try
                {
                await RevisarAsync(opciones, stoppingToken);
                }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                break;
                }
            catch (Exception ex)
                {
                // Excluido del buffer de errores: un fallo del servicio de
                // reparación no debe generarle un error al propio notificador.
                _logger.LogError(ex, "Ciclo de auto-reparación de la IA local falló; se reintentará en el próximo intervalo.");
                }

            var segundos = Math.Clamp(opciones.Sanacion.IntervaloSegundos, 15, 3600);
            try { await Task.Delay(TimeSpan.FromSeconds(segundos), stoppingToken); }
            catch (TaskCanceledException) { break; }
            }
        }

    private async Task RevisarAsync(OllamaOptions opciones, CancellationToken ct)
        {
        if (!opciones.Enabled || !opciones.Sanacion.Habilitada)
            return;

        if (!Uri.TryCreate(opciones.Url, UriKind.Absolute, out var uri) || !uri.IsLoopback)
            return; // Ollama en otra máquina: no se gestiona el proceso desde acá.

        // ¿Hay algo que verificar? (a) el monitoreo marcó el API caído, (b) hubo un
        // fallo de generación real nuevo, o (c) hay una reparación que no alcanzó
        // y sigue en curso.
        var apiCaida = !_connectionState.IsConnected;
        var falloNuevo = _connectionState.UltimoFalloEn.HasValue &&
                         (!_falloProcesado.HasValue || _connectionState.UltimoFalloEn.Value > _falloProcesado.Value);
        if (!apiCaida && !falloNuevo && !_reparacionFallida)
            return;

        if (!await _semaforo.WaitAsync(0, ct))
            return; // ya hay una reparación en curso

        try
            {
            var momentoFallo = DateTimeFromUtc(_connectionState.UltimoFalloEn);
            var motivo = falloNuevo
                ? $"la generación falló ({_connectionState.UltimoFalloMotivo ?? "sin detalle"})"
                : "el servicio no responde (sin conexión)";

            _logger.LogWarning(
                "Auto-reparación IA local: detectado un problema — {Motivo} (falla registrada {Momento:HH:mm:ss}). Verificando con una generación de prueba...",
                motivo, momentoFallo);

            var verificacion = await VerificarConReintentosAsync(opciones, ct);

            switch (verificacion.Diagnostico)
                {
                case Diagnostico.Ok:
                    // Falso positivo, se recuperó solo o los intentos de reparación
                    // terminaron funcionando: se cierra el incidente.
                    var reparadoTrasFallo = _reparacionFallida;
                    var ultimoFallo = _ultimoFalloReparacion;
                    LimpiarEstadoDeIncidente();
                    _falloProcesado = _connectionState.UltimoFalloEn;
                    _connectionState.IsConnected = true;
                    _connectionState.IsModelReady = true;
                    _logger.LogInformation(
                        "Auto-reparación IA local: la generación de prueba resultó OK ({Detalle}); no hizo falta reiniciar nada.",
                        verificacion.Detalle);
                    if (reparadoTrasFallo)
                        {
                        // Ya se le avisó el problema al administrador: cerrar el
                        // incidente con el correo de resolución.
                        await EnviarCorreoAsync(ConstruirAsunto(requerido: false), ConstruirCuerpo(
                            requerido: false, momentoFallo, motivo,
                            "el servicio volvió a responder tras los intentos de reparación",
                            verificacion.Detalle,
                            $"Último intento automático: {ultimoFallo ?? "sin detalle"}.",
                            $"Verificación final OK ({verificacion.Detalle}): la IA local volvió a funcionar. Los usuarios no lo notaron: mientras tanto, la IA web o los planes predefinidos atendieron los pedidos."), ct);
                        }
                    return;

                case Diagnostico.ErrorDeClientes:
                    // 4xx (modelo en descarga, nombre mal configurado…): lo resuelve
                    // el arranque/monitoreo existente; reiniciar no arreglaría nada.
                    LimpiarEstadoDeIncidente();
                    _falloProcesado = _connectionState.UltimoFalloEn;
                    _connectionState.IsConnected = true;
                    _logger.LogWarning(
                        "Auto-reparación IA local: la generación de prueba devolvió {Detalle}; es un error de cliente (no del proceso) y NO se reinicia. El arranque existente se encarga (p. ej. descarga del modelo).",
                        verificacion.Detalle);
                    return;

                case Diagnostico.Indeciso:
                    _falloProcesado = _connectionState.UltimoFalloEn;
                    _logger.LogWarning(
                        "Auto-reparación IA local: la verificación no pudo confirmar el estado ({Detalle}); se deja pasar este ciclo y se reevalúa ante el próximo fallo.",
                        verificacion.Detalle);
                    return;
                }

            // Confirmado: o la generación devuelve 5xx ("colgado") o no hay
            // conexión ("caído"). Ambos casos se reparan reiniciando el proceso.
            var esCaido = verificacion.Diagnostico == Diagnostico.SinConexion;
            var queEs = esCaido
                ? "el proceso no responde / conexión rechazada (posible caída)"
                : "el API responde pero la generación devuelve error del servidor (posible proceso colgado)";

            await RepararConAvisoAsync(opciones, momentoFallo, motivo, queEs, verificacion, ct);
            }
        finally
            {
            _semaforo.Release();
            }
        }

    private async Task RepararConAvisoAsync(
        OllamaOptions opciones,
        DateTime momentoFallo,
        string motivo,
        string queEs,
        ResultadoVerificacion verificacion,
        CancellationToken ct)
        {
        var sanacion = opciones.Sanacion;

        if (!sanacion.GestionarProceso)
            {
            // No se quiere reiniciar el proceso desde la app: un solo aviso por incidente.
            _reparacionFallida = true;
            if (!_avisoDeIntervencionEnviado)
                {
                _avisoDeIntervencionEnviado = true;
                _logger.LogWarning(
                    "Auto-reparación IA local: fallo confirmado pero GestionarProceso=false; se avisa al administrador y no se reinicia.");
                await EnviarCorreoAsync(ConstruirAsunto(requerido: true), ConstruirCuerpo(
                    requerido: true, momentoFallo, motivo, queEs, verificacion.Detalle,
                    "No gestionado por la app (Ollama:Sanacion:GestionarProceso=false).",
                    "Reiniciar el servicio de IA local en la máquina donde corre Ollama."), ct);
                }
            return;
            }

        LimpiarVentanaDeReinicios();
        var maximo = Math.Max(1, sanacion.MaxReiniciosPorHora);
        if (_intentos.Count >= maximo)
            {
            // Límite horario alcanzado: UN solo correo por incidente; después se
            // sigue reintentando en silencio (una ventana cada hora) hasta que
            // vuelva a funcionar o alguien lo arregle a mano.
            if (!_avisoDeIntervencionEnviado)
                {
                _avisoDeIntervencionEnviado = true;
                _logger.LogError(
                    "Auto-reparación IA local: fallo confirmado ({QueEs}) tras agotar el máximo de {Max} intento(s) por hora; se avisa al administrador y se sigue reintentando en silencio.",
                    queEs, maximo);
                await EnviarCorreoAsync(ConstruirAsunto(requerido: true), ConstruirCuerpo(
                    requerido: true, momentoFallo, motivo, queEs, verificacion.Detalle,
                    $"Se intentó reparar automáticamente y no alcanzó (límite de {maximo}/hora); último detalle: {_ultimoFalloReparacion ?? "sin detalle"}.",
                    $"Reiniciar el servicio de IA local en el servidor (por ejemplo: ollama serve, o el servicio de Windows si está instalado) y probar con: ollama run {opciones.Model} \"hola\". La app seguirá reintentando sola en segundo plano y, una vez reparado, la vuelve a usar sin más cambios."), ct);
                }
            return;
            }

        _intentos.Add(DateTime.UtcNow);

        _logger.LogInformation(
            "Auto-reparación IA local: reiniciando el servicio para resolver — {QueEs} (falla desde {Momento:HH:mm:ss}, verificación: {Detalle}).",
            queEs, momentoFallo, verificacion.Detalle);

        var resultado = await RepararAsync(opciones, ct);

        if (resultado.Exitosa)
            {
            LimpiarEstadoDeIncidente();
            _falloProcesado = _connectionState.UltimoFalloEn;
            _connectionState.IsConnected = true;
            _connectionState.IsModelReady = true;

            _logger.LogInformation(
                "Auto-reparación IA local: REPARADA — {Detalle}. La IA local volvió a estar disponible sin intervención del usuario.",
                resultado.Detalle);

            await EnviarCorreoAsync(ConstruirAsunto(requerido: false), ConstruirCuerpo(
                requerido: false, momentoFallo, motivo, queEs, verificacion.Detalle,
                resultado.Detalle,
                "Sin cambios para los usuarios: mientras tanto, la IA web o los planes predefinidos atendieron los pedidos y la ruta local ya volvió a estar disponible."), ct);
            }
        else
            {
            // Sin correo por cada intento fallido: se reintenta en los próximos
            // ciclos (sujeto al límite horario) y el detalle queda para el correo
            // único de "requiere intervención" si no se recupera.
            _reparacionFallida = true;
            _ultimoFalloReparacion = resultado.Detalle;

            if (verificacion.Diagnostico == Diagnostico.SinConexion)
                {
                _connectionState.IsConnected = false;
                _connectionState.IsModelReady = false;
                }
            else
                {
                _connectionState.IsConnected = true; // el API sí responde
                _connectionState.IsModelReady = false;
                }

            _logger.LogError(
                "Auto-reparación IA local: el intento no alcanzó — {Detalle}. Se reintenta en los próximos ciclos; los usuarios siguen cubiertos por la IA web / planes predefinidos.",
                resultado.Detalle);
            }
        }

    /// <summary>Repara el servicio: limpia procesos de ejecución residuales,
    /// detiene Ollama (si está) y/o lo vuelve a iniciar, espera a que el API
    /// responda y confirma con una generación de prueba.</summary>
    private async Task<ResultadoReparacion> RepararAsync(OllamaOptions opciones, CancellationToken ct)
        {
        var sanacion = opciones.Sanacion;
        var procesos = Process.GetProcessesByName("ollama");

        // Resolver la ruta ANTES de detener (MainModule solo se lee con el proceso vivo).
        var exe = ResolverRutaEjecutable(sanacion, procesos);

        // 1) Limpiar procesos de ejecución ("llama-server") residuales: un reinicio
        //    brusco del servicio puede dejar uno huérfano (proceso padre muerto) que
        //    responde HTTP 500 a toda generación aunque el API siga arriba — es el
        //    fallo real que se dio en pruebas. Se eliminan todos los locales: los
        //    del servicio se reinician de todos modos.
        var runnersLimpiados = 0;
        foreach (var r in Process.GetProcessesByName("llama-server"))
            {
            try
                {
                var pid = r.Id;
                r.Kill();
                runnersLimpiados++;
                _logger.LogInformation("Auto-reparación IA local: proceso de ejecución residual eliminado (PID {Pid}).", pid);
                }
            catch (Exception ex)
                {
                _logger.LogDebug(ex, "Auto-reparación IA local: no se pudo eliminar el proceso de ejecución residual (PID {Pid}).", r.Id);
                }
            finally
                {
                r.Dispose();
                }
            }
        if (runnersLimpiados > 0)
            await Task.Delay(TimeSpan.FromSeconds(1), ct);

        var reinicioDeProceso = false;
        if (procesos.Length > 0)
            {
            // 2) Detener el servicio (árbol completo: incluye su proceso de ejecución).
            var algunoDetenido = false;
            foreach (var p in procesos)
                {
                try
                    {
                    var pid = p.Id;
                    p.Kill(entireProcessTree: true);
                    algunoDetenido = true;
                    reinicioDeProceso = true;
                    _logger.LogInformation("Auto-reparación IA local: proceso detenido (PID {Pid}).", pid);
                    }
                catch (Exception ex)
                    {
                    _logger.LogWarning(ex, "Auto-reparación IA local: no se pudo detener el proceso (PID {Pid}); posible falta de permisos.", p.Id);
                    }
                finally
                    {
                    p.Dispose();
                    }
                }

            if (!algunoDetenido)
                return new(false, "no se pudo detener el proceso de Ollama (falta de permisos desde la app; verificar el usuario con el que corre el sitio).");

            // Esperar a que el API quede libre (máx ~15 s). Si un gestor tipo
            // NSSM/tray lo vuelve a levantar solo, mejor: no hace falta iniciarlo.
            var bajo = false;
            for (int i = 0; i < 8 && !bajo; i++)
                {
                if (!await EstaApiArribaAsync(opciones, ct)) { bajo = true; break; }
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
                }

            if (!bajo)
                return new(false, "el proceso siguió respondiendo tras el intento de detenerlo (falta de permisos o gestor externo).");
            }

        // 3) Pequeña pausa: si un gestor (NSSM / bandeja del sistema) ya lo relanzó,
        //    no lo iniciamos nosotros (evitaríamos un conflicto de puerto).
        await Task.Delay(TimeSpan.FromSeconds(1.5), ct);
        if (!await EstaApiArribaAsync(opciones, ct))
            {
            if (string.IsNullOrEmpty(exe))
                return new(false, "no se encontró la ruta de ollama.exe (configurar Ollama:Sanacion:RutaEjecutable con la ruta de instalación).");

            string? errorInicio = null;
            try
                {
                var inicio = new ProcessStartInfo(exe, "serve")
                    {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(exe) ?? string.Empty
                    };
                Process.Start(inicio)?.Dispose();
                reinicioDeProceso = true;
                _logger.LogInformation("Auto-reparación IA local: iniciando {Exe} serve...", exe);
                }
            catch (Exception ex)
                {
                errorInicio = $" (no se pudo iniciar {Path.GetFileName(exe)}: {ex.GetType().Name})";
                _logger.LogWarning(ex, "Auto-reparación IA local: no se pudo iniciar {Exe}.", exe);
                }

            // Esperar a que el API esté arriba (máx 60 s: incluye el arranque del proceso).
            var arriba = false;
            for (int i = 0; i < 30 && !arriba; i++)
                {
                if (await EstaApiArribaAsync(opciones, ct)) { arriba = true; break; }
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
                }

            if (!arriba)
                return new(false, $"el servicio no volvió a responder tras el reinicio (60 s){errorInicio}");
            }

        // 4) Confirmación final con generaciones de prueba reales (recién iniciado,
        //    Ollama puede devolver 500 mientras el modelo termina de cargar).
        var verificacion = await VerificarConReintentosAsync(opciones, ct);

        if (verificacion.Diagnostico == Diagnostico.Ok)
            {
            var acciones = new List<string>();
            if (reinicioDeProceso) acciones.Add("se reinició el proceso de Ollama");
            if (runnersLimpiados > 0) acciones.Add($"se eliminaron {runnersLimpiados} proceso(s) de ejecución del modelo en residuales");
            var accionTexto = acciones.Count > 0
                ? string.Join("; ", acciones)
                : "no hizo falta reiniciar el proceso";
            return new(true, $"{accionTexto} y la verificación posterior resultó OK ({verificacion.Detalle}: la IA local volvió a funcionar)");
            }

        return new(false, $"el servicio volvió a responder pero la verificación de generación sigue sin pasar ({verificacion.Detalle})");
        }

    /// <summary>Hasta 3 generaciones de prueba con 3 s de separación: recién
    /// iniciado, Ollama puede devolver 500 mientras el modelo termina de cargar.
    /// Un 4xx sí es definitivo (falta el modelo / configuración) y no se reintenta.</summary>
    private async Task<ResultadoVerificacion> VerificarConReintentosAsync(OllamaOptions opciones, CancellationToken ct)
        {
        ResultadoVerificacion? ultimo = null;
        for (int intento = 1; intento <= 3; intento++)
            {
            if (intento > 1)
                await Task.Delay(TimeSpan.FromSeconds(3), ct);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(opciones.Sanacion.TimeoutVerificacionSegundos, 10, 300)));
            ultimo = await VerificarGeneracionAsync(opciones, cts.Token);

            if (ultimo.Diagnostico is Diagnostico.Ok or Diagnostico.ErrorDeClientes)
                return ultimo;
            }

        return ultimo!;
        }

    /// <summary>Generación de prueba mínima: es la única forma de ver el "colgado",
    /// porque el API sigue respondiendo el listado de modelos con normalidad.
    /// Usa un cliente propio y sin techo de tiempo propio: el reloj lo pone el
    /// CTS del llamador (TimeoutVerificacionSegundos), independiente del
    /// TimeoutSeconds general de la ruta de IA.</summary>
    private async Task<ResultadoVerificacion> VerificarGeneracionAsync(OllamaOptions opciones, CancellationToken ct)
        {
        if (!Uri.TryCreate(opciones.Url, UriKind.Absolute, out var baseUri))
            return new(Diagnostico.Indeciso, "Ollama:Url inválida");

        try
            {
            using var client = new HttpClient { BaseAddress = baseUri, Timeout = Timeout.InfiniteTimeSpan };
            var solicitud = new
                {
                model = opciones.Model,
                messages = new[] { new { role = "user", content = "Respondi solo: ok" } },
                stream = false,
                options = new { num_ctx = 256, num_predict = 8 },
                keep_alive = string.IsNullOrWhiteSpace(opciones.KeepAlive) ? "30m" : opciones.KeepAlive
                };

            using var respuesta = await client.PostAsJsonAsync("/api/chat", solicitud, ct);
            var codigo = (int)respuesta.StatusCode;

            if (respuesta.IsSuccessStatusCode)
                return new(Diagnostico.Ok, $"HTTP {codigo}");
            if (codigo >= 500)
                return new(Diagnostico.ErrorDeGeneracion, $"HTTP {codigo}");
            return new(Diagnostico.ErrorDeClientes, $"HTTP {codigo}");
            }
        catch (HttpRequestException ex)
            {
            var causa = ex.InnerException?.GetType().Name ?? ex.GetType().Name;
            return new(Diagnostico.SinConexion, causa);
            }
        catch (OperationCanceledException)
            {
            // No se confirma nada: puede ser un modelo cargando con lentitud.
            return new(Diagnostico.Indeciso, "la verificación superó su tiempo de espera");
            }
        catch (Exception ex)
            {
            // Ante la duda no se reinicia: mejor perderse un ciclo que reiniciar sin confirmar.
            _logger.LogWarning(ex, "Auto-reparación IA local: la verificación falló de forma inesperada.");
            return new(Diagnostico.Indeciso, ex.GetType().Name);
            }
        }

    /// <summary>¿El API de Ollama responde ahora mismo? (GET /api/tags con techo de 3 s).</summary>
    private async Task<bool> EstaApiArribaAsync(OllamaOptions opciones, CancellationToken ct)
        {
        try
            {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(3));
            using var client = _httpFactory.CreateClient("Ollama");
            using var respuesta = await client.GetAsync("/api/tags", cts.Token);
            return respuesta.IsSuccessStatusCode;
            }
        catch (HttpRequestException)
            {
            return false;
            }
        catch (OperationCanceledException)
            {
            return false;
            }
        catch (InvalidOperationException)
            {
            return false;
            }
        }

    /// <summary>
    /// Ruta de ollama.exe, sin rutas absolutas quemadas en el código (portátil
    /// para producción):
    ///   1. Ollama:Sanacion:RutaEjecutable — configuración explícita, la vía
    ///      recomendada en producción;
    ///   2. el ejecutable del proceso que está corriendo (la binaria exacta en uso);
    ///   3. "ollama.exe" en cualquier directorio del PATH (el instalador de Ollama
    ///      lo deja en el PATH del sistema o del usuario).
    /// Devuelve null si no se encuentra: la reparación no podrá iniciar el
    /// proceso y lo avisa con la instrucción de configurar la ruta.
    /// </summary>
    private string? ResolverRutaEjecutable(OllamaSanacionOptions sanacion, Process[] procesos)
        {
        if (!string.IsNullOrWhiteSpace(sanacion.RutaEjecutable))
            {
            if (File.Exists(sanacion.RutaEjecutable))
                return sanacion.RutaEjecutable;

            _logger.LogWarning(
                "Auto-reparación IA local: Ollama:Sanacion:RutaEjecutable ('{Ruta}') no existe; se busca el ejecutable por otra vía.",
                sanacion.RutaEjecutable);
            }

        foreach (var p in procesos)
            {
            try
                {
                var ruta = p.MainModule?.FileName;
                if (!string.IsNullOrEmpty(ruta) && File.Exists(ruta))
                    return ruta;
                }
            catch
                {
                // Sin permisos para leer el módulo: se sigue con el PATH.
                }
            }

        return BuscarEnPath("ollama.exe");
        }

    /// <summary>Busca un ejecutable en los directorios del PATH del sistema/usuario
    /// (sin depender de rutas fijas de una máquina concreta).</summary>
    private static string? BuscarEnPath(string nombreEjecutable)
        {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
            try
                {
                var ruta = Path.Combine(dir.Trim().Trim('"'), nombreEjecutable);
                if (File.Exists(ruta))
                    return ruta;
                }
            catch (ArgumentException)
                {
                // Entrada inválida del PATH: se ignora.
                }
            }

        return null;
        }

    private void LimpiarVentanaDeReinicios()
        {
        var corte = DateTime.UtcNow - VentanaDeReinicios;
        _intentos.RemoveAll(t => t < corte);
        }

    /// <summary>Cierra el incidente activo (éxito o fallo ya resuelto): se puede
    /// volver a avisar si ocurre otro.</summary>
    private void LimpiarEstadoDeIncidente()
        {
        _reparacionFallida = false;
        _ultimoFalloReparacion = null;
        _avisoDeIntervencionEnviado = false;
        }

    private static DateTime DateTimeFromUtc(DateTime? utc) =>
        utc.HasValue ? utc.Value.ToLocalTime() : DateTime.Now;

    private static string ConstruirAsunto(bool requerido) =>
        requerido
            ? $"Guarino App: IA local de Ollama requiere intervención ({DateTime.Now:dd/MM HH:mm})"
            : $"Guarino App: IA local de Ollama reparada automáticamente ({DateTime.Now:dd/MM HH:mm})";

    private string ConstruirCuerpo(
        bool requerido,
        DateTime momentoFallo,
        string motivo,
        string queEs,
        string verificacion,
        string accion,
        string resultado)
        {
        var opciones = _optionsMonitor.CurrentValue;
        var sb = new StringBuilder();
        sb.AppendLine("GUARINO APP - Auto-reparación de la IA local (informe automático)");
        sb.AppendLine($"Generado: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine("== QUÉ OCURRIÓ ==");
        sb.AppendLine($"· Falla registrada: {momentoFallo:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"· Síntoma: {motivo}.");
        sb.AppendLine($"· Diagnóstico: {queEs}.");
        sb.AppendLine($"· Servicio: {opciones.Url} · Modelo: {opciones.Model}");
        sb.AppendLine($"· Verificación con generación de prueba: {verificacion}.");
        sb.AppendLine();
        sb.AppendLine("== ACCIÓN AUTOMÁTICA ==");
        sb.AppendLine($"· {accion}");
        sb.AppendLine();
        sb.AppendLine("== ESTADO FINAL ==");
        sb.AppendLine($"· {(requerido ? "REQUIERE ATENCIÓN" : "REPARADO")} — {resultado}");
        sb.AppendLine();
        sb.AppendLine("Este es un mensaje automático de Guarino App. Destinatario único: el administrador de la aplicación.");
        return sb.ToString();
        }

    private async Task EnviarCorreoAsync(string asunto, string cuerpo, CancellationToken ct)
        {
        var opciones = _notificaciones.CurrentValue;
        if (!opciones.Habilitadas)
            {
            _logger.LogDebug("Correo de auto-reparación de la IA local omitido (Notificaciones:Habilitadas=false).");
            return;
            }

        var destino = (opciones.EmailAdmin ?? string.Empty).Trim();
        if (!EsEmailValido(destino))
            {
            _logger.LogWarning(
                "Correo de auto-reparación NO enviado: Notificaciones:EmailAdmin ('{EmailAdmin}') no es una dirección válida.", opciones.EmailAdmin);
            return;
            }

        try
            {
            using var scope = _scopeFactory.CreateScope();
            var email = scope.ServiceProvider.GetRequiredService<Application.Common.Interfaces.IEmailService>();
            await email.SendEmailAsync(destino, asunto, cuerpo);
            _logger.LogInformation("Correo de auto-reparación de la IA local enviado a {Destino} con asunto '{Asunto}'.", destino, asunto);
            }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
            throw;
            }
        catch (Exception ex)
            {
            // El emailService ya registra el error de envío en su log.
            _logger.LogError(ex, "No se pudo enviar el correo de auto-reparación de la IA local.");
            }
        }

    private static bool EsEmailValido(string email) =>
        !string.IsNullOrWhiteSpace(email) &&
        email.Contains('@') &&
        !email.Contains(' ') &&
        email.LastIndexOf('@') > 0 &&
        email.IndexOf('.', email.LastIndexOf('@')) > 0;
    }

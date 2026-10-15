using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace GuarinoApp.Infrastructure.Services;

/// <summary>
/// Auto-reparación silenciosa de la BASE DE DATOS + informe al administrador.
/// Mismo contrato que OllamaRecuperacionService: detectar → reparar → verificar
/// → UN correo por incidente.
///
/// Escenario real que motivó este servicio (probado en desarrollo): la app no
/// podía conectar a LocalDB porque un modelo de IA residente dejó la máquina sin
/// memoria libre y la instancia quedó detenida/colgada — la aplicación moría en
/// el arranque (Migrate) y, en producción, con ~20 usuarios concurrentes en el
/// asistente IA, una caída de la base dejaría la pantalla entera sin datos.
///
/// Qué hace (cada BaseDatos:Sanacion:IntervaloSegundos):
///   1. Verifica la conexión con una apertura real de conexión (conexión propia,
///      sin DbContext) — hasta 3 intentos con timeout corto por intento;
///   2. un corte transitorio (pico de carga de ~20 usuarios en el asistente) se
///      disuelve solo: el incidente se declara recién con
///      BaseDatos:Sanacion:CiclosDeConfirmacion ciclos consecutivos de fallo;
///   3. si el fallo se confirma y el destino es LOCAL, repara por ORDEN:
///      libera memoria (descarga modelos de IA residentes con keep_alive=0, que
///      fueron el desencadenante real) → levanta la instancia por INSTANCIA
///      (sqllocaldb start, y si no responde, sqllocaldb stop + start = limpiar
///      proceso colgado y relanzar limpio; para Motor=ServicioWindows, sc start,
///      con reinicio completo solo si ReiniciarServicio=true) → verifica de nuevo;
///   4. manda UN correo a Notificaciones:EmailAdmin: "reparada automáticamente"
///      al recuperarse o "requiere intervención" si no se pudo (máx.
///      MaxReparacionesPorHora por ventana deslizante); después reintenta en
///      silencio cada hora hasta que vuelva a funcionar.
///
/// Reglas de seguridad:
///   · NUNCA se mata un PID aislado de sqlservr: da derechos insuficientes y
///     podría afectar a otras bases del servidor — la limpieza es SIEMPRE por
///     instancia (nuestra instancia de LocalDB / nuestro servicio de Windows);
///   · solo se gestiona cuando el destino de la conexión es local (loopback /
///     "." / nombre de esta máquina); un servidor remoto solo se verifica y se
///     avisa (allá hay que intervenir desde infraestructura);
///   · sin credenciales en logs ni correos (solo servidor/base);
///   · errores de este servicio: reintentados en el próximo ciclo (el correo de
///     un fallo propio nunca dispara otro correo — EmailService está excluido
///     del buffer de errores).
///
/// Además, Program.cs lo usa en el arranque de Development: si Migrate() no
/// puede conectar, SanearArranqueAsync() libera memoria / levanta la instancia
/// y se reintenta (4 intentos) antes de rendirse.
/// </summary>
public class BaseDatosRecuperacionService : BackgroundService
    {
    private static readonly TimeSpan VentanaDeReparaciones = TimeSpan.FromHours(1);

    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IOptionsMonitor<BaseDatosOptions> _optionsMonitor;
    private readonly IOptionsMonitor<NotificacionesOptions> _notificaciones;
    private readonly ILogger<BaseDatosRecuperacionService> _logger;

    // Estado del incidente activo.
    private bool _incidenteActivo;
    private DateTime? _incidenteDesde;
    private string? _ultimoDetalle;
    private int _fallosConsecutivos;

    // Reparaciones dentro de la ventana deslizante de una hora + aviso único.
    private readonly List<DateTime> _reparaciones = new();
    private bool _avisoDeIntervencionEnviado;

    // Evita solapar dos reparaciones (ciclo del servicio vs. arranque).
    private readonly SemaphoreSlim _semaforo = new(1, 1);

    public BaseDatosRecuperacionService(
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpFactory,
        IOptionsMonitor<BaseDatosOptions> optionsMonitor,
        IOptionsMonitor<NotificacionesOptions> notificaciones,
        ILogger<BaseDatosRecuperacionService> logger)
        {
        _configuration = configuration;
        _scopeFactory = scopeFactory;
        _httpFactory = httpFactory;
        _optionsMonitor = optionsMonitor;
        _notificaciones = notificaciones;
        _logger = logger;
        }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
        // Dejamos que el arranque (migraciones, seed, chequeo de Ollama) termine
        // antes de empezar a verificar, para no competir con la puesta en marcha.
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
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
                _logger.LogError(ex, "Ciclo de auto-reparación de la base de datos falló; se reintentará en el próximo intervalo.");
                }

            var segundos = Math.Clamp(opciones.Sanacion.IntervaloSegundos, 15, 3600);
            try { await Task.Delay(TimeSpan.FromSeconds(segundos), stoppingToken); }
            catch (TaskCanceledException) { break; }
            }
        }

    // -----------------------------------------------------------------------
    // Ciclo principal
    // -----------------------------------------------------------------------

    private async Task RevisarAsync(BaseDatosOptions opciones, CancellationToken ct)
        {
        if (!opciones.Sanacion.Habilitada)
            return;

        if (string.IsNullOrWhiteSpace(_configuration.GetConnectionString("DefaultConnection")))
            {
            _logger.LogWarning("Auto-reparación BD: falta ConnectionStrings:DefaultConnection; nada que verificar.");
            return;
            }

        var verificacion = await VerificarConReintentosAsync(opciones, ct);

        if (verificacion.Diagnostico == DiagnosticoBaseDatos.Ok)
            {
            if (_incidenteActivo)
                {
                _logger.LogInformation(
                    "Auto-reparación BD: REPARADA — la base volvió a responder (incidente desde {Desde:HH:mm:ss}; verificada {Ahora:HH:mm:ss}).",
                    _incidenteDesde ?? DateTime.Now, DateTime.Now);
                await EnviarCorreoAsync(
                    ConstruirAsunto(requerido: false),
                    ConstruirCuerpo(
                        requerido: false,
                        momentoFallo: _incidenteDesde ?? DateTime.Now,
                        motivo: _ultimoDetalle ?? "fallo previo de conexión",
                        destino: verificacion.Servidor,
                        accion: "Verificación automática de la conexión: OK.",
                        resultado: $"la base de datos volvió a responder y se verificó a las {DateTime.Now:HH:mm:ss}."),
                    ct);
                }
            LimpiarEstadoDeIncidente();
            return;
            }

        if (verificacion.Diagnostico == DiagnosticoBaseDatos.Indeciso)
            {
            _logger.LogWarning(
                "Auto-reparación BD: verificación no concluyente ({Detalle}); posible pico de carga: NO se actúa y se reintentará.",
                verificacion.Detalle);
            return;
            }

        // Fallo confirmado en ESTA verificación (3 intentos): se confirma el
        // incidente recién tras N ciclos consecutivos para no actuar por un corte
        // transitorio (p. ej. ~20 usuarios saturando el asistente IA un momento).
        _fallosConsecutivos++;
        if (_fallosConsecutivos < opciones.Sanacion.CiclosDeConfirmacion)
            {
            _logger.LogWarning(
                "Auto-reparación BD: '{Servidor}' no responde ({Detalle}). Posible caída: confirmación {Actual}/{Necesaria}; si persiste en el próximo ciclo se actúa.",
                verificacion.Servidor, verificacion.Detalle, _fallosConsecutivos, opciones.Sanacion.CiclosDeConfirmacion);
            return;
            }

        if (!_incidenteActivo)
            {
            _incidenteActivo = true;
            _incidenteDesde = DateTime.Now;
            }

        switch (verificacion.Diagnostico)
            {
            case DiagnosticoBaseDatos.Reparable:
                await RepararAsync(opciones, verificacion, ct);
                break;

            case DiagnosticoBaseDatos.RemotoNoGestionable:
                _logger.LogError(
                    "Auto-reparación BD: '{Servidor}' no responde y NO es local: no hay nada que gestionar desde esta máquina. Último error: {Detalle}",
                    verificacion.Servidor, verificacion.Detalle);
                await AvisarIntervencionUnaVezAsync(
                    opciones, verificacion,
                    "servidor remoto sin gestión posible desde la app: verificar red / estado de SQL desde infraestructura",
                    ct);
                break;

            case DiagnosticoBaseDatos.SoloAviso:
                _logger.LogError(
                    "Auto-reparación BD: destino local pero no gestionable ({Detalle}). Se verifica y se avisa sin tocar la instancia.",
                    verificacion.Detalle);
                await AvisarIntervencionUnaVezAsync(
                    opciones, verificacion,
                    "gestión de instancia deshabilitada o mal configurada: revisar BaseDatos:Sanacion (GestionarInstancia / Motor / NombreServicio)",
                    ct);
                break;
            }
        }

    /// <summary>Verifica la conexión con hasta 3 intentos (timeout corto por
    /// intento) y clasifica el destino para saber si es gestionable.</summary>
    private async Task<VerificacionBaseDatos> VerificarConReintentosAsync(BaseDatosOptions opciones, CancellationToken ct)
        {
        var destino = DestinoParaAvisos();
        string? ultimoError = null;
        int? ultimoNumeroSql = null;

        for (var intento = 1; intento <= 3; intento++)
            {
            try
                {
                // Verificación con una conexión PROPIA (sin DbContext ni scopes):
                // se abre contra la cadena vigente en configuración y se cierra.
                // Timeout corto por intento.
                var cadena = _configuration.GetConnectionString("DefaultConnection") ?? "";
                await using var conexion = new SqlConnection(cadena);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(opciones.Sanacion.TimeoutVerificacionSegundos, 3, 120)));
                await conexion.OpenAsync(cts.Token);
                return new(DiagnosticoBaseDatos.Ok, destino, "la conexión se abrió correctamente");
                }
            catch (SqlException ex)
                {
                ultimoError = $"SQL {ex.Number}: {ex.Message}";
                ultimoNumeroSql = ex.Number;
                }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                throw;
                }
            catch (OperationCanceledException)
                {
                ultimoError = $"timeout de {opciones.Sanacion.TimeoutVerificacionSegundos} s al abrir la conexión";
                }
            catch (Exception ex)
                {
                // Algo ajeno a la base (p. ej. saturación de la app con ~20
                // usuarios): sin diagnóstico confiable NO se actúa.
                return new(DiagnosticoBaseDatos.Indeciso, destino, $"{ex.GetType().Name}: {ex.Message}");
                }

            if (intento < 3)
                {
                try { await Task.Delay(TimeSpan.FromSeconds(8), ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                }
            }

        // 4060 = la instancia RESPONDE pero la base solicitada no existe/no abre:
        // reiniciar no arregla eso (nombre/permisos/estado de la base) → solo se
        // avisa; la instancia solo se gestiona cuando el destino no responde.
        if (ultimoNumeroSql == 4060)
            return new(DiagnosticoBaseDatos.SoloAviso, destino,
                $"{ultimoError}; la base solicitada no existe o no pudo abrirse (SQL 4060): revisar nombre/estado de la base — reiniciar la instancia no lo arregla");

        // 3 intentos fallidos → ¿el destino es gestionable desde acá?
        if (!EsDestinoLocal(ObtenerDataSource()))
            return new(DiagnosticoBaseDatos.RemotoNoGestionable, destino, ultimoError ?? "sin respuesta en 3 intentos");
        if (!opciones.Sanacion.GestionarInstancia)
            return new(DiagnosticoBaseDatos.SoloAviso, destino,
                $"{ultimoError}; gestión deshabilitada (BaseDatos:Sanacion:GestionarInstancia=false)");
        if (!MotorGestionable(opciones))
            return new(DiagnosticoBaseDatos.SoloAviso, destino,
                $"{ultimoError}; motor de gestión no utilizable (BaseDatos:Sanacion:Motor='{opciones.Sanacion.Motor}', NombreServicio='{opciones.Sanacion.NombreServicio}')");
        return new(DiagnosticoBaseDatos.Reparable, destino, ultimoError ?? "sin respuesta en 3 intentos");
        }

    /// <summary>Repara: libera memoria si hay presión, levanta la instancia por
    /// instancia y verifica. UN correo según el resultado.</summary>
    private async Task RepararAsync(BaseDatosOptions opciones, VerificacionBaseDatos verificacion, CancellationToken ct)
        {
        if (!await _semaforo.WaitAsync(TimeSpan.Zero, ct))
            return; // ya hay una reparación en curso; no se solapan.

        try
            {
            var enVentana = _reparaciones.Count(t => t > DateTime.Now - VentanaDeReparaciones);
            if (enVentana >= opciones.Sanacion.MaxReparacionesPorHora)
                {
                await AvisarIntervencionUnaVezAsync(
                    opciones, verificacion,
                    $"se agotó el límite de {opciones.Sanacion.MaxReparacionesPorHora} reparaciones por hora: el problema requiere intervención manual; la verificación sigue en cada ciclo",
                    ct);
                _logger.LogWarning(
                    "Auto-reparación BD: límite de {Max} reparaciones/hora alcanzado; sin más intentos hasta la próxima ventana (se sigue verificando en cada ciclo).",
                    opciones.Sanacion.MaxReparacionesPorHora);
                return;
                }

            _reparaciones.Add(DateTime.Now);
            var acciones = new List<string>();

            // 1) Memoria PRIMERO: el caso real fue un modelo de IA residente
            //    dejándole a SQL poca RAM para arrancar.
            if (opciones.Sanacion.LiberarMemoria)
                {
                var libre = MemoriaLibreMB();
                if (libre.HasValue && libre.Value < opciones.Sanacion.MemoriaMinimaLibreMB)
                    {
                    _logger.LogWarning(
                        "Auto-reparación BD: solo {Libre} MB de RAM libre (mínimo {Minimo} MB); se descargan los modelos de IA locales antes de tocar la base de datos.",
                        libre, opciones.Sanacion.MemoriaMinimaLibreMB);
                    acciones.Add($"Memoria: {await LiberarMemoriaDeIaAsync(ct)}");
                    }
                else
                    {
                    acciones.Add($"Memoria: {libre?.ToString() ?? "desconocida"} MB libres; no hizo falta liberar.");
                    }
                }

            // 2) Instancia por INSTANCIA (nunca un PID aislado).
            var gestion = await GestionarInstanciaAsync(opciones, ct);
            acciones.Add(gestion);
            _logger.LogInformation("Auto-reparación BD: {Gestion}", gestion);

            // 3) Verificación posterior.
            var posterior = await VerificarConReintentosAsync(opciones, ct);
            if (posterior.Diagnostico == DiagnosticoBaseDatos.Ok)
                {
                var accion = string.Join(" ", acciones);
                _logger.LogInformation(
                    "Auto-reparación BD: REPARADA — la base volvió a responder tras: {Accion}", accion);
                await EnviarCorreoAsync(
                    ConstruirAsunto(requerido: false),
                    ConstruirCuerpo(
                        requerido: false,
                        momentoFallo: _incidenteDesde ?? DateTime.Now,
                        motivo: _ultimoDetalle ?? verificacion.Detalle,
                        destino: posterior.Servidor,
                        accion: accion,
                        resultado: $"conexión restablecida y verificada a las {DateTime.Now:HH:mm:ss}."),
                    ct);
                LimpiarEstadoDeIncidente();
                }
            else
                {
                _ultimoDetalle = $"{posterior.Diagnostico}: {posterior.Detalle}";
                _logger.LogWarning(
                    "Auto-reparación BD: la reparación NO alcanzó todavía ({Detalle}); se reintentará en el próximo ciclo.",
                    _ultimoDetalle);
                }
            }
        finally
            {
            _semaforo.Release();
            }
        }

    /// <summary>UN correo "requiere intervención" por incidente (no por intento
    /// fallido): la primera vez que se llega a un estado no gestionable o se
    /// agota la ventana de reparaciones.</summary>
    private async Task AvisarIntervencionUnaVezAsync(
        BaseDatosOptions opciones, VerificacionBaseDatos verificacion, string motivoAccion, CancellationToken ct)
        {
        if (_avisoDeIntervencionEnviado)
            return;
        _avisoDeIntervencionEnviado = true;

        await EnviarCorreoAsync(
            ConstruirAsunto(requerido: true),
            ConstruirCuerpo(
                requerido: true,
                momentoFallo: _incidenteDesde ?? DateTime.Now,
                motivo: _ultimoDetalle ?? verificacion.Detalle,
                destino: verificacion.Servidor,
                accion: motivoAccion,
                resultado: "la base de datos sigue sin responder; se reintentará automáticamente en cada ciclo y se informará por correo si se recupera."),
            ct);
        }

    private void LimpiarEstadoDeIncidente()
        {
        _incidenteActivo = false;
        _incidenteDesde = null;
        _ultimoDetalle = null;
        _fallosConsecutivos = 0;
        _reparaciones.Clear();
        _avisoDeIntervencionEnviado = false;
        }

    // -----------------------------------------------------------------------
    // Acciones de reparación
    // -----------------------------------------------------------------------

    /// <summary>Uso puntual desde el arranque (Program.cs, Development): si
    /// Migrate() no puede conectar, deja la BD local en condiciones — libera
    /// memoria si hay presión y levanta la instancia — antes de reintentar.
    /// Sin correos ni contadores: los avisos los gestiona el ciclo regular.</summary>
    public async Task SanearArranqueAsync(CancellationToken ct)
        {
        var opciones = _optionsMonitor.CurrentValue;
        var dataSource = ObtenerDataSource();

        if (!EsDestinoLocal(dataSource))
            {
            _logger.LogWarning(
                "Auto-reparación BD (arranque): destino '{Destino}' no es local: no se gestiona nada desde esta máquina.",
                dataSource);
            return;
            }

        if (opciones.Sanacion.LiberarMemoria)
            {
            var libre = MemoriaLibreMB();
            if (libre.HasValue && libre.Value < opciones.Sanacion.MemoriaMinimaLibreMB)
                {
                _logger.LogWarning(
                    "Auto-reparación BD (arranque): solo {Libre} MB libres (mínimo {Minimo}); liberando memoria de la IA local...",
                    libre, opciones.Sanacion.MemoriaMinimaLibreMB);
                await LiberarMemoriaDeIaAsync(ct);
                }
            }

        var gestion = await GestionarInstanciaAsync(opciones, ct);
        _logger.LogInformation("Auto-reparación BD (arranque): {Gestion}", gestion);
        }

    /// <summary>Descarga los modelos de IA residentes en Ollama (keep_alive=0)
    /// para liberar RAM. Solo si Ollama apunta a loopback; si no responde se
    /// sigue igual con el resto de la reparación.</summary>
    private async Task<string> LiberarMemoriaDeIaAsync(CancellationToken ct)
        {
        try
            {
            var cliente = _httpFactory.CreateClient("Ollama");
            if (cliente.BaseAddress is null || !cliente.BaseAddress.IsLoopback)
                return "la IA local no apunta a loopback: no se tocó su memoria.";

            var ps = await cliente.GetAsync("/api/ps", ct);
            ps.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await ps.Content.ReadAsStringAsync(ct));

            var cargados = new List<string>();
            if (doc.RootElement.TryGetProperty("models", out var modelos) && modelos.ValueKind == JsonValueKind.Array)
                {
                foreach (var m in modelos.EnumerateArray())
                    {
                    var nombre = m.TryGetProperty("name", out var n) ? n.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(nombre))
                        cargados.Add(nombre);
                    }
                }

            if (cargados.Count == 0)
                return "no había modelos de IA residentes en memoria.";

            foreach (var nombre in cargados)
                {
                var descarga = await cliente.PostAsJsonAsync("/api/generate", new { model = nombre, keep_alive = 0 }, ct);
                descarga.EnsureSuccessStatusCode();
                _logger.LogInformation(
                    "Auto-reparación BD: modelo de IA '{Modelo}' descargado de memoria (keep_alive=0) para liberar RAM.", nombre);
                }

            return $"modelos de IA descargados de memoria: {string.Join(", ", cargados)}.";
            }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
            throw;
            }
        catch (Exception ex)
            {
            _logger.LogWarning(
                "Auto-reparación BD: no se pudo liberar memoria de la IA local ({Mensaje}); se continúa igualmente con la instancia.",
                ex.Message);
            return "la liberación de memoria de la IA local no fue posible (se intentó igual con la instancia).";
            }
        }

    /// <summary>Levanta la instancia gestionada, SIEMPRE por instancia:
    /// LocalDb → sqllocaldb start (y si no responde, sqllocaldb stop + start,
    /// que limpia el proceso colgado de ESA instancia y la relanza limpia);
    /// ServicioWindows → sc start (reinicio completo solo con ReiniciarServicio).
    /// Devuelve una descripción legible para el log y el correo.</summary>
    private async Task<string> GestionarInstanciaAsync(BaseDatosOptions opciones, CancellationToken ct)
        {
        var motor = (opciones.Sanacion.Motor ?? "").Trim();

        if (motor.Equals("LocalDb", StringComparison.OrdinalIgnoreCase))
            {
            var instancia = (opciones.Sanacion.NombreInstanciaLocalDb ?? "").Trim();
            if (instancia.Length == 0)
                instancia = "mssqllocaldb";

            var inicio = await EjecutarComandoAsync("sqllocaldb.exe", $"start \"{instancia}\"", ct);
            if (inicio.ExitCode == 0)
                return $"Instancia LocalDB '{instancia}' iniciada (sqllocaldb start).";

            _logger.LogWarning(
                "Auto-reparación BD: sqllocaldb start '{Instancia}' devolvió código {Codigo} ({Salida}); se detiene la instancia completa y se relanza limpia.",
                instancia, inicio.ExitCode, inicio.Salida);

            await EjecutarComandoAsync("sqllocaldb.exe", $"stop \"{instancia}\"", ct);
            var relanzo = await EjecutarComandoAsync("sqllocaldb.exe", $"start \"{instancia}\"", ct);
            return relanzo.ExitCode == 0
                ? $"la instancia LocalDB '{instancia}' no respondió al arranque simple: se detuvo (limpieza de proceso colgado de ESA instancia) y se relanzó limpia."
                : $"la instancia LocalDB '{instancia}' NO levantó (código {relanzo.ExitCode}: {relanzo.Salida}).";
            }

        if (motor.Equals("ServicioWindows", StringComparison.OrdinalIgnoreCase))
            {
            var servicio = (opciones.Sanacion.NombreServicio ?? "").Trim();
            if (servicio.Length == 0)
                return "Motor=ServicioWindows pero BaseDatos:Sanacion:NombreServicio está vacío: no se puede gestionar el servicio (completar la configuración).";

            if (opciones.Sanacion.ReiniciarServicio)
                {
                var detencion = await EjecutarComandoAsync("sc.exe", $"stop \"{servicio}\"", ct);
                // 1062 = ya estaba detenido; cualquier otro código no bloquea:
                // lo que importa es el arranque de abajo.
                _logger.LogInformation(
                    "Auto-reparación BD: sc stop '{Servicio}' → código {Codigo} ({Salida})",
                    servicio, detencion.ExitCode, detencion.Salida);
                try { await Task.Delay(TimeSpan.FromSeconds(5), ct); }
                catch (OperationCanceledException) { throw; }
                }

            var arranque = await EjecutarComandoAsync("sc.exe", $"start \"{servicio}\"", ct);
            return arranque.ExitCode == 0
                ? $"servicio '{servicio}' iniciado{(opciones.Sanacion.ReiniciarServicio ? " (reinicio completo: sc stop + sc start)" : "")}."
                : $"el servicio '{servicio}' no respondió al arranque (código {arranque.ExitCode}: {arranque.Salida}); requiere intervención manual con privilegios (consola de servicios o sc stop/start).";
            }

        return $"Motor '{motor}' no reconocido (usar LocalDb, ServicioWindows o SoloAvisar): no se gestionó la instancia.";
        }

    /// <summary>Ejecuta un comando de sistema de forma silenciosa con timeout,
    /// capturando código de salida y salida (para log/correo).</summary>
    private static async Task<(int ExitCode, string Salida)> EjecutarComandoAsync(string archivo, string argumentos, CancellationToken ct)
        {
        var psi = new ProcessStartInfo(archivo, argumentos)
            {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
            };

        using var proceso = Process.Start(psi)
            ?? throw new InvalidOperationException($"No se pudo iniciar '{archivo}'.");

        // Se leen ambos streams en paralelo para que no se saturen los búferes.
        var salidaTask = proceso.StandardOutput.ReadToEndAsync();
        var errorTask = proceso.StandardError.ReadToEndAsync();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(60));
        try
            {
            await proceso.WaitForExitAsync(cts.Token);
            }
        catch (OperationCanceledException)
            {
            try { proceso.Kill(entireProcessTree: true); } catch { /* ya terminó */ }
            ct.ThrowIfCancellationRequested();
            return (-1, $"timeout de 60 s ejecutando '{archivo} {argumentos}' (proceso terminado por seguridad).");
            }

        var salida = (await salidaTask).Trim();
        var error = (await errorTask).Trim();
        var detalle = string.IsNullOrWhiteSpace(error) ? salida : $"{salida} {error}".Trim();
        return (proceso.ExitCode, detalle);
        }

    // -----------------------------------------------------------------------
    // Diagnóstico del destino
    // -----------------------------------------------------------------------

    /// <summary>DataSource crudo de la cadena de conexión (vacío si no se pudo
    /// interpretar).</summary>
    private string ObtenerDataSource()
        {
        try
            {
            return new SqlConnectionStringBuilder(_configuration.GetConnectionString("DefaultConnection") ?? "").DataSource?.Trim() ?? "";
            }
        catch
            {
            return "";
            }
        }

    /// <summary>Solo servidor/base para logs y correos — NUNCA credenciales.</summary>
    private string DestinoParaAvisos()
        {
        try
            {
            var b = new SqlConnectionStringBuilder(_configuration.GetConnectionString("DefaultConnection") ?? "");
            return string.IsNullOrWhiteSpace(b.InitialCatalog)
                ? b.DataSource ?? "(desconocido)"
                : $"{b.DataSource} / base '{b.InitialCatalog}'";
            }
        catch
            {
            return "(cadena de conexión no interpretable)";
            }
        }

    /// <summary>¿El destino está en esta máquina? Solo así se gestiona el proceso
    /// o la instancia; un servidor remoto únicamente se verifica y se avisa.
    /// El DataSource puede traer "tcp:" y ",puerto" (p. ej. "tcp:127.0.0.1,1433"
    /// o "127.0.0.1,9999"): se normaliza antes de comparar, o un local con puerto
    /// explícito se confundiría con un remoto.</summary>
    private static bool EsDestinoLocal(string servidor)
        {
        var s = (servidor ?? "").Trim();
        if (s.Length == 0)
            return false;

        if (s.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
            s = s.Substring(4).Trim();
        var comaPuerto = s.IndexOf(',');
        if (comaPuerto >= 0)
            s = s.Substring(0, comaPuerto).Trim();
        if (s.Length == 0)
            return false;

        if (s.StartsWith("(localdb)", StringComparison.OrdinalIgnoreCase))
            return true;
        if (s.Equals(".", StringComparison.Ordinal) ||
            s.Equals("(local)", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("127.0.0.1", StringComparison.Ordinal))
            return true;
        if (s.StartsWith(@".\", StringComparison.Ordinal) ||
            s.StartsWith(@"localhost\", StringComparison.OrdinalIgnoreCase) ||
            s.StartsWith(@"127.0.0.1\", StringComparison.Ordinal) ||
            s.StartsWith("(local)\\", StringComparison.OrdinalIgnoreCase))
            return true;
        var equipo = Environment.MachineName;
        return s.Equals(equipo, StringComparison.OrdinalIgnoreCase) ||
               s.StartsWith(equipo + "\\", StringComparison.OrdinalIgnoreCase);
        }

    /// <summary>¿El motor configurado permite gestionar la instancia?</summary>
    private static bool MotorGestionable(BaseDatosOptions opciones)
        {
        var motor = (opciones.Sanacion.Motor ?? "").Trim();
        if (motor.Equals("SoloAvisar", StringComparison.OrdinalIgnoreCase))
            return false;
        if (motor.Equals("LocalDb", StringComparison.OrdinalIgnoreCase))
            return true;
        if (motor.Equals("ServicioWindows", StringComparison.OrdinalIgnoreCase))
            return !string.IsNullOrWhiteSpace(opciones.Sanacion.NombreServicio);
        return false;
        }

    /// <summary>Memoria física disponible (MB) vía GlobalMemoryStatusEx;
    /// null si la llamada nativa falla.</summary>
    private static long? MemoriaLibreMB()
        {
        var estatus = new EstatusMemoriaWindows();
        return GlobalMemoryStatusEx(ref estatus)
            ? (long)(estatus.UllAvailPhys / (1024ul * 1024ul))
            : null;
        }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref EstatusMemoriaWindows lpBuffer);

    // -----------------------------------------------------------------------
    // Correo al administrador (UN por incidente)
    // -----------------------------------------------------------------------

    private static string ConstruirAsunto(bool requerido) =>
        requerido
            ? $"Guarino App: Base de datos requiere intervención ({DateTime.Now:dd/MM HH:mm})"
            : $"Guarino App: Base de datos reparada automáticamente ({DateTime.Now:dd/MM HH:mm})";

    private static string ConstruirCuerpo(
        bool requerido,
        DateTime momentoFallo,
        string motivo,
        string destino,
        string accion,
        string resultado)
        {
        var sb = new StringBuilder();
        sb.AppendLine("GUARINO APP - Auto-reparación de la base de datos (informe automático)");
        sb.AppendLine($"Generado: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine("== QUÉ OCURRIÓ ==");
        sb.AppendLine($"· Falla detectada: {momentoFallo:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine("· Síntoma: la aplicación no pudo abrir la conexión a la base de datos.");
        sb.AppendLine($"· Diagnóstico: {motivo}.");
        sb.AppendLine($"· Destino: {destino} (solo servidor/base, sin credenciales).");
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
            _logger.LogDebug("Correo de auto-reparación de la BD omitido (Notificaciones:Habilitadas=false).");
            return;
            }

        var destino = (opciones.EmailAdmin ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(destino) || !destino.Contains('@') || !destino.Contains('.'))
            {
            _logger.LogWarning(
                "Correo de auto-reparación de la BD NO enviado: Notificaciones:EmailAdmin ('{EmailAdmin}') no es una dirección válida.",
                opciones.EmailAdmin);
            return;
            }

        try
            {
            using var scope = _scopeFactory.CreateScope();
            var email = scope.ServiceProvider.GetRequiredService<Application.Common.Interfaces.IEmailService>();
            await email.SendEmailAsync(destino, asunto, cuerpo);
            _logger.LogInformation(
                "Correo de auto-reparación de la BD enviado a {Destino} con asunto '{Asunto}'.", destino, asunto);
            }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
            throw;
            }
        catch (Exception ex)
            {
            // El EmailService ya registra el error de envío en su log (y su
            // categoría está excluida del buffer: un fallo de correo no dispara
            // otro correo).
            _logger.LogWarning("No se pudo enviar el correo de auto-reparación de la BD: {Mensaje}", ex.Message);
            }
        }
    }

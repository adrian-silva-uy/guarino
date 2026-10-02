using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace GuarinoApp.Infrastructure.Services;

/// <summary>
/// Reporte de operación SOLO para el administrador de la app (único
/// destinatario: Notificaciones:EmailAdmin). Cada Notificaciones:IntervaloMinutos
/// revisa:
///   1. errores y avisos críticos registrados en el log desde el último reporte,
///   2. salud de la IA local (servicio y modelo — si falta o se cae, es una
///      actualización/reparación a realizar) y de la IA web (clave y modelo),
///   3. actualizaciones pendientes de base de datos (migraciones).
/// Si hay novedades manda UN correo con asunto
/// "Guarino App - logs del día : fecha_hora". El primer ciclo siempre manda el
/// resumen de arranque (sirve para confirmar que el correo llega). Si el envío
/// falla, los pendientes se conservan y se reintentan en el próximo ciclo.
/// </summary>
public class NotificadorAdminService : BackgroundService
    {
    private const int MaxPendientes = 400;
    private const int MaxLineasDeErrores = 40;
    private const int MaxCaracteresExcepcion = 800;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IOptionsMonitor<NotificacionesOptions> _notificaciones;
    private readonly IOptionsMonitor<OllamaOptions> _ollama;
    private readonly IOptionsMonitor<IaWebOptions> _iaWeb;
    private readonly OllamaConnectionState _ollamaEstado;
    private readonly ErrorLogBuffer _buffer;
    private readonly ILogger<NotificadorAdminService> _logger;

    // Cambios de estado de salud todavía no enviados (se limpian al enviar OK).
    private readonly List<string> _saludPendiente = new();

    // Errores del log todavía no enviados.
    private readonly List<ErrorLogEntry> _pendientes = new();

    private readonly Dictionary<string, string> _ultimoEstado = new();
    private bool _primerCiclo = true;

    public NotificadorAdminService(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpFactory,
        IOptionsMonitor<NotificacionesOptions> notificaciones,
        IOptionsMonitor<OllamaOptions> ollama,
        IOptionsMonitor<IaWebOptions> iaWeb,
        OllamaConnectionState ollamaEstado,
        ErrorLogBuffer buffer,
        ILogger<NotificadorAdminService> logger)
        {
        _scopeFactory = scopeFactory;
        _httpFactory = httpFactory;
        _notificaciones = notificaciones;
        _ollama = ollama;
        _iaWeb = iaWeb;
        _ollamaEstado = ollamaEstado;
        _buffer = buffer;
        _logger = logger;
        }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
        var opciones = _notificaciones.CurrentValue;
        if (opciones.Habilitadas)
            _logger.LogInformation(
                "Reportes de operación activos hacia {Destino} cada {Minutos} min (asunto '{Asunto}…fecha_hora').",
                opciones.EmailAdmin, Math.Max(5, opciones.IntervaloMinutos), opciones.Asunto);

        // Primer reporte a los 40 s (arranque + errores tempranos); después,
        // cada Notificaciones:IntervaloMinutos.
        try { await Task.Delay(TimeSpan.FromSeconds(40), stoppingToken); }
        catch (TaskCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
            {
            try
                {
                await RevisarYNotificarAsync(stoppingToken);
                }
            catch (OperationCanceledException)
                {
                break;
                }
            catch (Exception ex)
                {
                // Excluido del buffer de errores: el notificador no se notifica
                // a sí mismo (evita bucles de correo).
                _logger.LogError(ex, "Ciclo del reporte de operación falló; se reintentará en el próximo intervalo.");
                }

            var minutos = Math.Max(5, _notificaciones.CurrentValue.IntervaloMinutos);
            try { await Task.Delay(TimeSpan.FromMinutes(minutos), stoppingToken); }
            catch (TaskCanceledException) { break; }
            }
        }

    private async Task RevisarYNotificarAsync(CancellationToken ct)
        {
        var opciones = _notificaciones.CurrentValue;
        if (!opciones.Habilitadas)
            return;

        var destino = (opciones.EmailAdmin ?? string.Empty).Trim();
        if (!EsEmailValido(destino))
            {
            _logger.LogWarning(
                "Reportes de operación NO enviados: Notificaciones:EmailAdmin ('{EmailAdmin}') no es una dirección válida. " +
                "Configurá Notificaciones:EmailAdmin para activarlos.", opciones.EmailAdmin);
            return;
            }

        // 1) Errores y avisos acumulados en el log desde el último reporte.
        var errores = _buffer.Vaciar();
        if (errores.Count > 0)
            {
            _pendientes.AddRange(errores);
            if (_pendientes.Count > MaxPendientes)
                _pendientes.RemoveRange(0, _pendientes.Count - MaxPendientes);
            }

        // 2) Salud y actualizaciones: solo se notifican CAMBIOS de estado; el
        //    primer ciclo agrega el estado completo como resumen de arranque.
        var salud = await EvaluarSaludAsync(ct);
        for (int i = 0; i < salud.Count; i++)
            {
            var clave = ClaveDeSalud(salud, i);
            var texto = salud[i];
            var cambio = !_ultimoEstado.TryGetValue(clave, out var anterior) || anterior != texto;
            _ultimoEstado[clave] = texto;
            if (cambio || _primerCiclo)
                if (!_saludPendiente.Contains(texto))
                    _saludPendiente.Add(texto);
            }
        _primerCiclo = false;

        // 3) ¿Hay algo que contar?
        if (_pendientes.Count == 0 && _saludPendiente.Count == 0)
            {
            _logger.LogDebug("Reporte de operación: sin novedades en este ciclo.");
            return;
            }

        var cuerpo = ConstruirCuerpo(_pendientes, _saludPendiente);
        if (await IntentarEnviarAsync(destino, cuerpo, ct))
            {
            _pendientes.Clear();
            _saludPendiente.Clear();
            }
        // Si el envío falló, pendientes y salud quedan guardados para reintentar
        // en el próximo ciclo (EmailService ya registró el error en su log).
        }

    /// <summary>Evalúa IA local, IA web y base de datos; devuelve una línea de
    /// estado por componente (texto estable para detectar cambios).</summary>
    private async Task<List<string>> EvaluarSaludAsync(CancellationToken ct)
        {
        var lineas = new List<string>();

        // --- IA local (servicio y modelo) ---
        var ollama = _ollama.CurrentValue;
        if (!ollama.Enabled)
            lineas.Add("IA local (servicio): DESHABILITADA por configuración (Ollama:Enabled=false).");
        else if (!_ollamaEstado.IsConnected)
            lineas.Add($"IA local (servicio): SIN RESPUESTA en {ollama.Url} — verificar que el servicio de IA esté instalado y corriendo (posible actualización o reparación).");
        else if (!_ollamaEstado.IsModelReady)
            lineas.Add($"IA local (modelo): ACTUALIZACIÓN NECESARIA — falta descargar '{ollama.Model}'. Se reintenta solo; también puede descargarse con: ollama pull {ollama.Model}");
        else
            lineas.Add($"IA local (modelo): OK — '{ollama.Model}' disponible en {ollama.Url}.");

        // --- IA web (plan gratuito): clave y modelo contra el servicio ---
        lineas.AddRange(await EvaluarIaWebAsync(ct));

        // --- Base de datos: migraciones pendientes = actualización por aplicar ---
        lineas.Add(await EvaluarBaseDeDatosAsync(ct));

        return lineas;
        }

    private async Task<List<string>> EvaluarIaWebAsync(CancellationToken ct)
        {
        var ia = _iaWeb.CurrentValue;
        if (!ia.Habilitada || string.IsNullOrWhiteSpace(ia.ApiKey))
            return new List<string> { "IA web (plan gratuito): SIN CLAVE — la app funciona solo por la ruta local (configurar IaWeb:ApiKey / IaWeb__ApiKey activa la ruta web)." };

        try
            {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(12));

            using var request = new HttpRequestMessage(HttpMethod.Get, ia.UrlBase.TrimEnd('/') + "/models");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ia.ApiKey);

            using var cliente = _httpFactory.CreateClient();
            using var respuesta = await cliente.SendAsync(request, cts.Token);

            var codigo = (int)respuesta.StatusCode;
            if (codigo is 401 or 403)
                return new List<string> { $"IA web (plan gratuito): CLAVE RECHAZADA ({codigo}) — actualizar la clave IaWeb:ApiKey (consola del proveedor)." };

            if (!respuesta.IsSuccessStatusCode)
                return new List<string> { $"IA web (plan gratuito): el servicio respondió {codigo} — verificar el servicio web configurado (IaWeb:UrlBase)." };

            var json = await respuesta.Content.ReadAsStringAsync(cts.Token);
            var modelos = ExtraerIdsDeModelos(json);

            if (modelos.Count > 0 && !modelos.Any(m => m.Equals(ia.Modelo, StringComparison.OrdinalIgnoreCase)))
                return new List<string> { $"IA web (modelo): ACTUALIZACIÓN NECESARIA — '{ia.Modelo}' ya no está disponible en el servicio; elegir otro de: {string.Join(", ", modelos.Take(4))}." };

            return new List<string> { $"IA web (plan gratuito): OK — clave aceptada{(modelos.Count > 0 ? $", modelo '{ia.Modelo}' disponible" : string.Empty)}." };
            }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
            throw;
            }
        catch (OperationCanceledException)
            {
            return new List<string> { "IA web (plan gratuito): SIN RESPUESTA (se superó el tiempo de espera) — reintentará en el próximo ciclo." };
            }
        catch (Exception ex)
            {
            _logger.LogWarning("No se pudo verificar la IA web: {Mensaje}", ex.Message);
            return new List<string> { "IA web (plan gratuito): NO SE PUDO VERIFICAR (sin conexión al servicio) — reintentará en el próximo ciclo." };
            }
        }

    private async Task<string> EvaluarBaseDeDatosAsync(CancellationToken ct)
        {
        try
            {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<Data.ApplicationDbContext>();
            var pendientes = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();

            if (pendientes.Count == 0)
                return "Base de datos: OK — sin actualizaciones pendientes.";

            return $"Base de datos: ACTUALIZACIÓN NECESARIA — {pendientes.Count} migración(es) pendiente(s): {string.Join(", ", pendientes.Take(5))}. Aplicar antes del próximo despliegue.";
            }
        catch (OperationCanceledException)
            {
            throw;
            }
        catch (Exception ex)
            {
            _logger.LogError(ex, "No se pudo verificar la base de datos para el reporte de operación.");
            return $"Base de datos: NO SE PUDO VERIFICAR ({ex.GetType().Name}) — revisar conexión y configuración.";
            }
        }

    private async Task<bool> IntentarEnviarAsync(string destino, string cuerpo, CancellationToken ct)
        {
        try
            {
            using var scope = _scopeFactory.CreateScope();
            var email = scope.ServiceProvider.GetRequiredService<Application.Common.Interfaces.IEmailService>();

            var asunto = $"{_notificaciones.CurrentValue.Asunto}{DateTime.Now:yyyy-MM-dd HH:mm}";
            await email.SendEmailAsync(destino, asunto, cuerpo);

            _logger.LogInformation("Reporte de operación enviado a {Destino} con asunto '{Asunto}'.", destino, asunto);
            return true;
            }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
            throw;
            }
        catch (Exception ex)
            {
            _logger.LogError(ex, "No se pudo enviar el reporte de operación al administrador; se reintentará en el próximo ciclo.");
            return false;
            }
        }

    private static string ConstruirCuerpo(IReadOnlyList<ErrorLogEntry> errores, IReadOnlyList<string> salud)
        {
        var sb = new StringBuilder();
        sb.AppendLine("GUARINO APP - Reporte de operación para el administrador");
        sb.AppendLine($"Generado: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine($"RESUMEN: {errores.Count} error(es)/aviso(s) de log · {salud.Count} novedad(es) de salud o actualización");
        sb.AppendLine();

        if (errores.Count > 0)
            {
            sb.AppendLine($"== ERRORES Y AVISOS DEL LOG ({errores.Count}) ==");
            var mostrados = Math.Min(errores.Count, MaxLineasDeErrores);
            for (int i = 0; i < mostrados; i++)
                {
                var e = errores[i];
                sb.AppendLine();
                sb.AppendLine($"[{i + 1}] {e.Timestamp:yyyy-MM-dd HH:mm:ss} · {UltimoSegmento(e.Categoria)}");
                sb.AppendLine($"    {e.Mensaje}");
                if (!string.IsNullOrWhiteSpace(e.Excepcion))
                    {
                    var texto = e.Excepcion.Length > MaxCaracteresExcepcion
                        ? e.Excepcion[..MaxCaracteresExcepcion] + "…"
                        : e.Excepcion;
                    foreach (var linea in texto.Split('\n').Take(12))
                        sb.AppendLine("    " + linea.TrimEnd('\r'));
                    }
                }
            if (errores.Count > mostrados)
                sb.AppendLine($"+ {errores.Count - mostrados} registro(s) más (se conservan para el próximo reporte).");
            sb.AppendLine();
            }

        if (salud.Count > 0)
            {
            sb.AppendLine("== SALUD Y ACTUALIZACIONES ==");
            foreach (var linea in salud)
                sb.AppendLine("· " + linea);
            sb.AppendLine();
            }

        sb.AppendLine("Este es un mensaje automático de Guarino App. Destinatario único: el administrador de la aplicación.");
        return sb.ToString();
        }

    /// <summary>Clave estable por línea de salud (solo cambia si el orden de las
    /// líneas cambia; se usa para detectar transiciones de estado).</summary>
    private static string ClaveDeSalud(List<string> salud, int indice)
        {
        var texto = salud[indice];
        var corte = texto.IndexOf(':');
        return corte > 0 ? texto[..corte] : texto;
        }

    private static string UltimoSegmento(string categoria) =>
        categoria.Contains('.') ? categoria[(categoria.LastIndexOf('.') + 1)..] : categoria;

    private static bool EsEmailValido(string email) =>
        !string.IsNullOrWhiteSpace(email) &&
        email.Contains('@') &&
        !email.Contains(' ') &&
        email.LastIndexOf('@') > 0 &&
        email.IndexOf('.', email.LastIndexOf('@')) > 0;

    private static List<string> ExtraerIdsDeModelos(string json)
        {
        var ids = new List<string>();
        try
            {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                return ids;
            foreach (var item in data.EnumerateArray())
                {
                if (item.ValueKind == JsonValueKind.Object &&
                    item.TryGetProperty("id", out var id) &&
                    id.ValueKind == JsonValueKind.String)
                    ids.Add(id.GetString() ?? string.Empty);
                }
            }
        catch (JsonException)
            {
            // Respuesta inesperada: se trata como "sin info de modelos".
            }
        return ids;
        }
    }

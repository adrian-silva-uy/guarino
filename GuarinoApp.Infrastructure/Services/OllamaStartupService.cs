using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OllamaSharp;

namespace GuarinoApp.Infrastructure.Services;

/// <summary>
/// 1. Al arrancar la aplicación, reintenta conectarse a Ollama varias veces
///    (por si el servicio de Windows todavía está iniciando, ej. tras un
///    reinicio del servidor) y descarga el modelo si falta.
/// 2. A partir de ahí, sigue monitoreando la conexión cada
///    Ollama:HealthCheckIntervalSeconds mientras la app está corriendo.
///    Esto es clave en producción: si Ollama se cae o se recupera DESPUÉS
///    del arranque, OllamaConnectionState (y por lo tanto el chip de la UI
///    y las decisiones de IAIService) reflejan el estado real en vez de
///    quedar "congelados" con el resultado del primer chequeo.
///
/// Esto NO reemplaza tener Ollama corriendo como Servicio de Windows (eso es
/// a nivel de infraestructura, ver NSSM). Esto asegura que, una vez Ollama
/// esté arriba, el modelo correcto también lo esté, y que la app se entere
/// si Ollama se cae o vuelve, sin necesidad de reiniciar el sitio.
/// </summary>
public class OllamaStartupService : BackgroundService
    {
    private readonly IOptionsMonitor<OllamaOptions> _optionsMonitor;
    private readonly OllamaConnectionState _connectionState;
    private readonly ILogger<OllamaStartupService> _logger;

    // Evita el "pull storm": antes, si el chequeo de modelo fallaba (ej. porque
    // el nombre local no matchea EXACTO con el configurado -- típico cuando
    // Ollama guarda el modelo con un sufijo de cuantización distinto), se
    // reintentaba una descarga COMPLETA cada HealthCheckIntervalSeconds (30s
    // por defecto) sin límite. Eso satura a Ollama justo cuando alguien
    // intenta usarlo de verdad -- se ve como "se conecta y al instante se
    // desconecta". Ahora se espera este cooldown entre intentos de pull.
    private static readonly TimeSpan CooldownEntreIntentosDePull = TimeSpan.FromMinutes(5);
    private DateTime? _ultimoIntentoDePull;

    // Evita que dos chequeos se superpongan si uno tarda más que el intervalo
    // configurado (ej. un pull real en curso cuando llega el próximo tick).
    private readonly SemaphoreSlim _semaforoChequeo = new(1, 1);

    public OllamaStartupService(
        IOptionsMonitor<OllamaOptions> optionsMonitor,
        OllamaConnectionState connectionState,
        ILogger<OllamaStartupService> logger)
        {
        _optionsMonitor = optionsMonitor;
        _connectionState = connectionState;
        _logger = logger;
        }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
        var options = _optionsMonitor.CurrentValue;

        if (!options.Enabled)
            {
            _logger.LogInformation("Integración con Ollama deshabilitada (Ollama:Enabled = false). No se inicia el monitoreo.");
            return;
            }

        // Chequeo inicial con reintentos, igual que antes.
        await EnsureOllamaReadyAsync(stoppingToken);

        // Monitoreo continuo mientras la app viva.
        while (!stoppingToken.IsCancellationRequested)
            {
            var currentOptions = _optionsMonitor.CurrentValue;
            var intervalo = TimeSpan.FromSeconds(Math.Max(5, currentOptions.HealthCheckIntervalSeconds));

            try
                {
                await Task.Delay(intervalo, stoppingToken);
                }
            catch (TaskCanceledException)
                {
                break;
                }

            if (!currentOptions.Enabled)
                continue;

            await CheckHealthAsync(currentOptions, stoppingToken);
            }
        }

    /// <summary>Chequeo puntual de salud, usado en el bucle de monitoreo continuo.</summary>
    private async Task CheckHealthAsync(OllamaOptions options, CancellationToken ct)
        {
        if (string.IsNullOrWhiteSpace(options.Url) || string.IsNullOrWhiteSpace(options.Model))
            return;

        // Si el chequeo anterior todavía está corriendo (ej. un pull real en
        // curso), no arrancamos otro en paralelo -- simplemente lo salteamos
        // esta vuelta.
        if (!await _semaforoChequeo.WaitAsync(0, ct))
            {
            _logger.LogDebug("Se saltea este chequeo de Ollama: el anterior todavía está en curso.");
            return;
            }

        try
            {
            using var client = new OllamaApiClient(new Uri(options.Url)) { SelectedModel = options.Model };
            var estaCorriendo = await client.IsRunningAsync(ct);

            if (estaCorriendo)
                {
                if (!_connectionState.IsConnected)
                    _logger.LogInformation("Ollama volvió a estar disponible en {Url}.", options.Url);

                _connectionState.IsConnected = true;

                // Si el modelo configurado cambió en caliente (appsettings o
                // variable de entorno, ej. "llama3.2:1b" → una versión nueva),
                // se baja de "listo" hasta verificar/descargar el nuevo. El
                // modelo viejo queda en el servidor por si hay que volver.
                var modeloAnterior = _connectionState.ModeloVerificado;
                if (_connectionState.IsModelReady &&
                    !string.Equals(modeloAnterior, options.Model, StringComparison.Ordinal))
                    {
                    _logger.LogInformation(
                        "Ollama:Model cambió ({Anterior} → {Nuevo}): se verifica o descarga el nuevo modelo. El anterior permanece instalado por si hay que volver.",
                        modeloAnterior, options.Model);
                    _connectionState.IsModelReady = false;
                    _connectionState.ModeloVerificado = null;
                    }

                if (!_connectionState.IsModelReady)
                    await AsegurarModeloDescargadoAsync(client, options.Model, ct);
                }
            else
                {
                if (_connectionState.IsConnected)
                    _logger.LogWarning("Ollama dejó de responder en {Url} (servidor no disponible). Cambiando a modo sin IA generativa.", options.Url);

                _connectionState.IsConnected = false;
                }
            }
        catch (Exception ex)
            {
            if (_connectionState.IsConnected)
                _logger.LogWarning(ex, "Ollama dejó de responder en {Url} (excepción en el healthcheck). Cambiando a modo sin IA generativa.", options.Url);

            _connectionState.IsConnected = false;
            }
        finally
            {
            _semaforoChequeo.Release();
            }
        }

    private async Task EnsureOllamaReadyAsync(CancellationToken ct)
        {
        var options = _optionsMonitor.CurrentValue;

        if (string.IsNullOrWhiteSpace(options.Url) || string.IsNullOrWhiteSpace(options.Model))
            {
            _logger.LogError("Ollama:Url o Ollama:Model no están configurados. No se puede verificar la conexión.");
            return;
            }

        using var client = new OllamaApiClient(new Uri(options.Url)) { SelectedModel = options.Model };

        const int maxIntentos = 12;
        var espera = TimeSpan.FromSeconds(10);

        for (int intento = 1; intento <= maxIntentos; intento++)
            {
            try
                {
                if (await client.IsRunningAsync(ct))
                    {
                    _logger.LogInformation("Ollama detectado en {Url} (intento {Intento}/{Max}).", options.Url, intento, maxIntentos);
                    _connectionState.IsConnected = true;
                    await AsegurarModeloDescargadoAsync(client, options.Model, ct);
                    return;
                    }
                }
            catch (Exception ex)
                {
                _logger.LogWarning(ex, "Ollama no responde todavía en {Url} (intento {Intento}/{Max}).", options.Url, intento, maxIntentos);
                }

            await Task.Delay(espera, ct);
            }

        _connectionState.IsConnected = false;
        _connectionState.IsModelReady = false;
        _logger.LogError(
            "No se pudo conectar a Ollama en {Url} tras {Max} intentos ({Segundos}s en total). " +
            "Verificá que el Servicio de Windows de Ollama esté instalado y corriendo (Get-Service OllamaService). " +
            "La app funcionará en modo de respuestas predefinidas mientras tanto; el monitoreo continuo seguirá " +
            "reintentando cada {Intervalo}s.",
            options.Url, maxIntentos, maxIntentos * espera.TotalSeconds, options.HealthCheckIntervalSeconds);
        }

    private async Task AsegurarModeloDescargadoAsync(OllamaApiClient client, string model, CancellationToken ct)
        {
        // Si ya intentamos un pull hace poco y seguimos acá (falló o el match
        // de nombre no dio positivo), esperamos el cooldown antes de volver a
        // intentar -- en vez de disparar una descarga completa en cada
        // healthcheck. Esto es lo que corta el "pull storm" que coincidía con
        // los cortes reportados.
        if (_ultimoIntentoDePull.HasValue && DateTime.UtcNow - _ultimoIntentoDePull.Value < CooldownEntreIntentosDePull)
            {
            _logger.LogDebug(
                "Se saltea el reintento de verificación/descarga del modelo '{Model}': todavía en cooldown (último intento hace {Minutos:0.0} min).",
                model, (DateTime.UtcNow - _ultimoIntentoDePull.Value).TotalMinutes);
            return;
            }

        _ultimoIntentoDePull = DateTime.UtcNow;

        try
            {
            var modelos = await client.ListLocalModelsAsync(ct);
            var yaDescargado = modelos.Any(m =>
                m.Name.Equals(model, StringComparison.OrdinalIgnoreCase) ||
                m.Name.StartsWith(model.Split(':')[0] + ":", StringComparison.OrdinalIgnoreCase));

            if (yaDescargado)
                {
                _logger.LogInformation("Modelo '{Model}' ya está descargado. Todo listo.", model);
                _connectionState.IsModelReady = true;
                _connectionState.ModeloVerificado = model;
                return;
                }

            // Log detallado de qué modelos SÍ están localmente, para poder
            // diagnosticar un mismatch de nombre sin tener que entrar al
            // servidor a mano (ej. "llama3.2:1b" configurado vs
            // "llama3.2:1b-instruct-q4_K_M" instalado realmente).
            _logger.LogWarning(
                "Modelo configurado '{Model}' no matcheó con ninguno de los modelos locales detectados: [{ModelosLocales}]. " +
                "Si el modelo SÍ está instalado mirá si el nombre configurado en Ollama:Model coincide exactamente " +
                "(incluyendo el tag después de los dos puntos). Iniciando descarga automática de '{Model}'...",
                model, string.Join(", ", modelos.Select(m => m.Name)), model);

            var ultimoPorcentajeLogueado = -1;
            await foreach (var status in client.PullModelAsync(model, ct))
                {
                if (status is null) continue;

                int porcentajeActual = (int)status.Percent;

                if (porcentajeActual / 10 != ultimoPorcentajeLogueado / 10)
                    {
                    ultimoPorcentajeLogueado = porcentajeActual;
                    _logger.LogInformation("Descargando '{Model}': {Status} ({Percent:0}%)", model, status.Status, status.Percent);
                    }
                }

            _logger.LogInformation("Modelo '{Model}' descargado correctamente. La IA generativa ya está lista para usarse.", model);
            _connectionState.IsModelReady = true;
            _connectionState.ModeloVerificado = model;
            }
        catch (Exception ex)
            {
            _connectionState.IsModelReady = false;
            _connectionState.ModeloVerificado = null;
            _logger.LogError(ex,
                "Error al verificar/descargar el modelo '{Model}'. Puede descargarse manualmente en el servidor con: ollama pull {Model}. " +
                "Se reintentará automáticamente en {Cooldown} minutos.",
                model, model, CooldownEntreIntentosDePull.TotalMinutes);
            }
        }
    }

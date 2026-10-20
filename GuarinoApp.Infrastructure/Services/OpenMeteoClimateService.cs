using System.Globalization;
using System.Text.Json;
using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Settings;
using GuarinoApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;

namespace GuarinoApp.Infrastructure.Services
    {
    internal class LoteInfo
        {
        public int Id { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public int ProductorId { get; set; }
        }

    public class OpenMeteoClimateService : IClimateService
        {
        // Candado estático (compartido por todas las instancias, sin importar si el servicio
        // se resuelve como Scoped): evita que dos escaneos corran en paralelo y dupliquen eventos
        // cuando coincide un clic manual con el disparo del ClimateMonitoringHostedService.
        private static readonly SemaphoreSlim _scanLock = new(1, 1);

        private readonly HttpClient _httpClient;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<OpenMeteoClimateService> _logger;
        private readonly ClimateRiskSettings _settings;
        private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;
        private readonly IAsyncPolicy<HttpResponseMessage> _circuitBreakerPolicy;

        public OpenMeteoClimateService(
            HttpClient httpClient,
            IServiceScopeFactory scopeFactory,
            ILogger<OpenMeteoClimateService> logger,
            IOptions<ClimateRiskSettings> settings)
            {
            _httpClient = httpClient;
            _scopeFactory = scopeFactory;
            _logger = logger;
            _settings = settings.Value;

            // Política de reintentos con backoff exponencial
            _retryPolicy = Policy
                .Handle<HttpRequestException>()
                .Or<TaskCanceledException>()
                .OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode && (int)r.StatusCode >= 500)
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        _logger.LogWarning(
                            "Reintento {RetryCount} después de {Delay}ms para URL {Url}",
                            retryCount,
                            timespan.TotalMilliseconds,
                            context.ContainsKey("url") ? context["url"] : "desconocida");
                    });

            // Circuit breaker: si hay 5 fallos consecutivos en 30 segundos, espera 60 segundos
            _circuitBreakerPolicy = Policy
                .Handle<HttpRequestException>()
                .OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode && (int)r.StatusCode >= 500)
                .CircuitBreakerAsync<HttpResponseMessage>(
                    handledEventsAllowedBeforeBreaking: 5,
                    durationOfBreak: TimeSpan.FromSeconds(60),
                    onBreak: (outcome, duration) =>
                    {
                        _logger.LogError(
                            "Circuit breaker activado. Se reintentará en {Seconds} segundos",
                            duration.TotalSeconds);
                    },
                    onReset: () =>
                    {
                        _logger.LogInformation("Circuit breaker reseteado. Llamadas a OpenMeteo restablecidas.");
                    });
            }

        public async Task DetectarYRegistrarEventosAsync(CancellationToken cancellationToken)
            {
            // Si ya hay un escaneo en curso (manual o del background service), no arrancamos otro:
            // evita duplicar eventos cuando ambos disparos coinciden en el tiempo.
            if (!await _scanLock.WaitAsync(0, cancellationToken))
                {
                _logger.LogWarning("Escaneo climático solicitado pero ya hay uno en curso. Se omite esta ejecución.");
                return;
                }

            try
                {
                await EjecutarEscaneoAsync(cancellationToken);
                }
            finally
                {
                _scanLock.Release();
                }
            }

        private async Task EjecutarEscaneoAsync(CancellationToken cancellationToken)
            {
            // Scope propio: el escaneo dura minutos (HTTP con reintentos) y corre
            // tanto desde el hosted service como desde la página (vía command);
            // con el DbContext del circuito compartido chocaban las cargas en vuelo.
            using var scope = _scopeFactory.CreateScope();
            var _context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

            var thresholds = _settings.Thresholds;
            var impacto = _settings.ImpactEstimates;

            // Guardia anti-inventos: si la config de umbrales/impactos viene en cero
            // (sección ausente o binding roto), el escáner dispararía "eventos" sobre
            // clima normal —el 15/10/2026 una corrida con config en cero registró
            // "LluviaExcesiva Crítica" con 0 mm y "VientoFuerte Crítica" con 14 km/h,
            // todos con 0% de pérdida— y esos datos falsos se sumaban a los KPIs.
            // Ante duda sobre la configuración: no se registra ningún evento
            // (mejor ausencia de dato que dato falso).
            if (EsConfigClimaticaInvalida(thresholds, impacto))
                {
                _logger.LogError(
                    "Configuración ClimateRisk inválida (umbrales o impactos en cero). Escaneo cancelado sin registrar eventos: revisá appsettings (ClimateRisk:Thresholds / ClimateRisk:ImpactEstimates).");
                return;
                }

            _logger.LogInformation("Iniciando escaneo automático de eventos climáticos ({RegionalContext})...", _settings.RegionalContext);

            var lotes = await _context.Lotes
                .Where(l => l.Latitude != null && l.Longitude != null)
                .Select(l => new LoteInfo
                    {
                    Id = l.Id,
                    Codigo = l.Codigo,
                    Latitude = l.Latitude,
                    Longitude = l.Longitude,
                    ProductorId = l.ProductorId
                    })
                .ToListAsync(cancellationToken);

            if (!lotes.Any())
                {
                _logger.LogWarning("No hay lotes con coordenadas. Escaneo detenido.");
                return;
                }

            // Solo nos importa la ventana de días que efectivamente consultamos a Open-Meteo (past_days=3 + hoy)
            var fechaDesde = DateTime.Today.AddDays(-4);

            // Claves de eventos ya existentes en BD para esa ventana, para no duplicar entre corridas del escaneo
            var eventosExistentes = await _context.EventosClimaticos
                .Where(e => e.Fecha >= fechaDesde && !e.IsDeleted)
                .Select(e => new { e.LoteId, e.TipoEvento, e.Fecha })
                .ToListAsync(cancellationToken);

            var clavesExistentes = new HashSet<(int LoteId, string TipoEvento, DateTime Fecha)>(
                eventosExistentes.Select(e => (e.LoteId, e.TipoEvento, e.Fecha)));

            var allEventos = new List<EventoClimatico>();
            var semaphore = new SemaphoreSlim(_settings.MaxParallelRequests, _settings.MaxParallelRequests);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_settings.ApiTimeoutSeconds * lotes.Count + 60));

            var tasks = lotes.Select(async lote =>
            {
                await semaphore.WaitAsync(cts.Token);
                try
                    {
                    var eventos = await ProcesarLoteAsync(lote, thresholds, impacto, cts.Token);

                    // Descartar eventos que ya existen en BD para ese Lote/Tipo/Fecha (evita duplicar entre escaneos)
                    var eventosNuevos = eventos
                        .Where(e => !clavesExistentes.Contains((e.LoteId, e.TipoEvento, e.Fecha)))
                        .ToList();

                    lock (allEventos)
                        {
                        if (eventosNuevos.Any())
                            {
                            allEventos.AddRange(eventosNuevos);
                            _logger.LogInformation("Lote {Codigo}: {Cantidad} eventos nuevos detectados.", lote.Codigo, eventosNuevos.Count);
                            }
                        }
                    }
                catch (Exception ex)
                    {
                    _logger.LogError(ex, "Error procesando clima para Lote {LoteId}", lote.Id);
                    }
                finally
                    {
                    semaphore.Release();
                    }
            });

            await Task.WhenAll(tasks);

            if (allEventos.Any())
                {
                await CalcularKilosPerdidosAsync(allEventos, cancellationToken);
                await _context.EventosClimaticos.AddRangeAsync(allEventos, cancellationToken);
                }

            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Escaneo climático finalizado. Eventos nuevos insertados: {Total}.", allEventos.Count);
            }

        /// <summary>
        /// true si falta configuración esencial: un umbral o porcentaje de impacto en
        /// cero invalida todo el escáner (todo clima normal dispararía eventos y el
        /// daño saldría multiplicado por cero). HeladaTempMin/HeladaTempSevera no se
        /// validan: 0 °C es un umbral legítimo de helada.
        /// </summary>
        private static bool EsConfigClimaticaInvalida(ThresholdsConfig thresholds, ImpactEstimatesConfig impacto)
            => thresholds.LluviaExcesivaMm <= 0
            || thresholds.LluviaMuyExcesivaMm <= 0
            || thresholds.VientoFuerteKmh <= 0
            || thresholds.VientoFuerteKmhCritico <= 0
            || thresholds.CalorExtremoTempMax <= 0
            || thresholds.GranizoWeatherCodeMin <= 0
            || thresholds.GranizoWeatherCodeSevera <= 0
            || impacto.HeladaSeveraPerdida <= 0
            || impacto.HeladaPerdida <= 0
            || impacto.LluviaExcesivaPerdida <= 0
            || impacto.LluviaMuyExcesivaPerdida <= 0
            || impacto.VientoFuertePerdida <= 0
            || impacto.CalorExtremoPerdida <= 0
            || impacto.GranizoPerdida <= 0
            || impacto.GranizoSeveraPerdida <= 0;

        /// <summary>
        /// Completa el indicador derivado "kilos perdidos" al momento de persistir:
        /// porcentaje estimado × kilos del lote. Es un snapshot del evento (si el
        /// lote cambia después, el histórico no se reescribe). Antes el campo
        /// quedaba en NULL porque ningún escritor lo calculaba y el KPI del
        /// dashboard sumaba cero.
        /// </summary>
        private async Task CalcularKilosPerdidosAsync(
            List<EventoClimatico> eventos,
            CancellationToken cancellationToken)
            {
            // Lectura AsNoTracking independiente: scope propio (no comparte el
            // DbContext del escaneo ni el del circuito que lo disparó).
            using var scope = _scopeFactory.CreateScope();
            var _context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

            var loteIds = eventos.Select(e => e.LoteId).Distinct().ToList();
            var kilosPorLote = await _context.Lotes
                .AsNoTracking()
                .Where(l => loteIds.Contains(l.Id))
                .ToDictionaryAsync(l => l.Id, l => l.KilosNetos, cancellationToken);

            foreach (var ev in eventos)
                {
                if (ev.PorcentajePerdidaEstimada is not decimal porcentaje) continue;
                if (!kilosPorLote.TryGetValue(ev.LoteId, out var kilos)) continue;
                ev.KilosPerdidosEstimados = Math.Round(kilos * porcentaje / 100m, 2);
                }
            }

        private async Task<List<EventoClimatico>> ProcesarLoteAsync(
            LoteInfo lote,
            ThresholdsConfig thresholds,
            ImpactEstimatesConfig impacto,
            CancellationToken cancellationToken)
            {
            var latitudeStr = lote.Latitude?.ToString(CultureInfo.InvariantCulture);
            var longitudeStr = lote.Longitude?.ToString(CultureInfo.InvariantCulture);

            // past_days=3: eventos ya ocurridos, para evaluar daño real sobre la cosecha.
            // forecast_days=7: pronóstico a futuro, para poder actuar preventivamente
            // (riego antiheladas, adelantar cosecha antes de lluvia fuerte, etc.) antes de que
            // el evento impacte la fruta exportable. Antes dependía del default no documentado
            // de Open-Meteo (7 días); ahora queda explícito en el código.
            var url = $"{_settings.OpenMeteoApiUrl}?latitude={latitudeStr}&longitude={longitudeStr}" +
                "&hourly=temperature_2m,precipitation,wind_speed_10m,weather_code&timezone=auto&past_days=3&forecast_days=7";

            try
                {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(_settings.ApiTimeoutSeconds));

                var response = await _httpClient.GetAsync(url, cts.Token);
                if (!response.IsSuccessStatusCode)
                    {
                    _logger.LogWarning("API OpenMeteo retornó {StatusCode} para lote {LoteId}", response.StatusCode, lote.Id);
                    return [];
                    }

                var content = await response.Content.ReadAsStringAsync(cts.Token);
                var climaData = JsonSerializer.Deserialize<OpenMeteoResponse>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (climaData?.Hourly == null || climaData.Hourly.Time.Count == 0)
                    {
                    _logger.LogWarning("Datos inválidos de OpenMeteo para lote {LoteId}", lote.Id);
                    return [];
                    }

                return DetectarEventos(lote, climaData, thresholds, impacto);
                }
            catch (OperationCanceledException)
                {
                _logger.LogWarning("Timeout en llamada a OpenMeteo para lote {LoteId}", lote.Id);
                return [];
                }
            catch (Exception ex)
                {
                _logger.LogError(ex, "Excepción al procesar lote {LoteId}", lote.Id);
                return [];
                }
            }

        private List<EventoClimatico> DetectarEventos(
            LoteInfo lote,
            OpenMeteoResponse climaData,
            ThresholdsConfig thresholds,
            ImpactEstimatesConfig impacto)
            {
            var eventosDetectados = new List<EventoClimatico>();
            var registrosProcesados = new HashSet<(string tipo, DateTime fecha)>();

            for (int i = 0; i < climaData.Hourly.Time.Count; i++)
                {
                var fechaHora = climaData.Hourly.Time[i];
                var temp = climaData.Hourly.Temperature_2m[i] ?? 999;
                var lluvia = climaData.Hourly.Precipitation[i] ?? 0;
                var viento = climaData.Hourly.Wind_speed_10m[i] ?? 0;
                var weatherCode = (i < climaData.Hourly.Weather_code.Count)
                    ? climaData.Hourly.Weather_code[i]
                    : null;

                DetectarHelada(lote, thresholds, impacto, fechaHora, temp, eventosDetectados, registrosProcesados);
                DetectarLluvia(lote, thresholds, impacto, fechaHora, lluvia, eventosDetectados, registrosProcesados);
                DetectarViento(lote, thresholds, impacto, fechaHora, viento, eventosDetectados, registrosProcesados);
                DetectarCalor(lote, thresholds, impacto, fechaHora, temp, eventosDetectados, registrosProcesados);
                DetectarGranizo(lote, thresholds, impacto, fechaHora, weatherCode, eventosDetectados, registrosProcesados);
                }

            return eventosDetectados;
            }

        private void DetectarHelada(
            LoteInfo lote,
            ThresholdsConfig thresholds,
            ImpactEstimatesConfig impacto,
            DateTime fechaHora,
            double temp,
            List<EventoClimatico> eventos,
            HashSet<(string, DateTime)> procesados)
            {
            if (temp > thresholds.HeladaTempMin) return;

            var key = ("Helada", fechaHora.Date);
            if (procesados.Contains(key)) return;

            var severidad = temp <= thresholds.HeladaTempSevera ? "Crítica" : "Alta";
            var perdida = temp <= thresholds.HeladaTempSevera ? impacto.HeladaSeveraPerdida : impacto.HeladaPerdida;

            eventos.Add(new EventoClimatico
                {
                LoteId = lote.Id,
                ProductorId = lote.ProductorId,
                Fecha = fechaHora.Date,
                TipoEvento = "Helada",
                SeveridadEstimada = severidad,
                PorcentajePerdidaEstimada = (decimal?)perdida,
                Fuente = $"Detección Automática - OpenMeteo ({_settings.RegionalContext})",
                Observaciones = $"Temp mínima: {temp}°C a las {fechaHora.Hour}:00. Riesgo de daño a frutos."
                });
            procesados.Add(key);
            }

        private void DetectarLluvia(
            LoteInfo lote,
            ThresholdsConfig thresholds,
            ImpactEstimatesConfig impacto,
            DateTime fechaHora,
            double lluvia,
            List<EventoClimatico> eventos,
            HashSet<(string, DateTime)> procesados)
            {
            if (lluvia < thresholds.LluviaExcesivaMm) return;

            var key = ("LluviaExcesiva", fechaHora.Date);
            if (procesados.Contains(key)) return;

            var severidad = lluvia >= thresholds.LluviaMuyExcesivaMm ? "Crítica" : "Alta";
            var perdida = lluvia >= thresholds.LluviaMuyExcesivaMm ? impacto.LluviaMuyExcesivaPerdida : impacto.LluviaExcesivaPerdida;

            eventos.Add(new EventoClimatico
                {
                LoteId = lote.Id,
                ProductorId = lote.ProductorId,
                Fecha = fechaHora.Date,
                TipoEvento = "LluviaExcesiva",
                SeveridadEstimada = severidad,
                PorcentajePerdidaEstimada = (decimal?)perdida,
                Fuente = "Detección Automática - OpenMeteo",
                Observaciones = $"Acumulado 24h: {lluvia}mm. Riesgo de gomosis."
                });
            procesados.Add(key);
            }

        private void DetectarViento(
            LoteInfo lote,
            ThresholdsConfig thresholds,
            ImpactEstimatesConfig impacto,
            DateTime fechaHora,
            double viento,
            List<EventoClimatico> eventos,
            HashSet<(string, DateTime)> procesados)
            {
            if (viento < thresholds.VientoFuerteKmh) return;

            var key = ("VientoFuerte", fechaHora.Date);
            if (procesados.Contains(key)) return;

            var severidad = viento >= thresholds.VientoFuerteKmhCritico ? "Crítica" : "Alta";

            eventos.Add(new EventoClimatico
                {
                LoteId = lote.Id,
                ProductorId = lote.ProductorId,
                Fecha = fechaHora.Date,
                TipoEvento = "VientoFuerte",
                SeveridadEstimada = severidad,
                PorcentajePerdidaEstimada = (decimal?)impacto.VientoFuertePerdida,
                Fuente = "Detección Automática - OpenMeteo",
                Observaciones = $"Viento máximo: {viento} km/h. Riesgo de ramas/fruta."
                });
            procesados.Add(key);
            }

        private void DetectarCalor(
            LoteInfo lote,
            ThresholdsConfig thresholds,
            ImpactEstimatesConfig impacto,
            DateTime fechaHora,
            double temp,
            List<EventoClimatico> eventos,
            HashSet<(string, DateTime)> procesados)
            {
            if (temp < thresholds.CalorExtremoTempMax) return;

            var key = ("CalorExtremo", fechaHora.Date);
            if (procesados.Contains(key)) return;

            eventos.Add(new EventoClimatico
                {
                LoteId = lote.Id,
                ProductorId = lote.ProductorId,
                Fecha = fechaHora.Date,
                TipoEvento = "CalorExtremo",
                SeveridadEstimada = "Alta",
                PorcentajePerdidaEstimada = (decimal?)impacto.CalorExtremoPerdida,
                Fuente = "Detección Automática - OpenMeteo",
                Observaciones = $"Temp máxima: {temp}°C a las {fechaHora.Hour}:00. Riesgo de golpe de sol en fruta."
                });
            procesados.Add(key);
            }

        /// <summary>
        /// Granizo por código meteorológico WMO (weather_code de Open-Meteo):
        /// 96 = tormenta con granizo leve, 99 = granizo intenso. Es el evento que más
        /// daño mecánico provoca en fruta y follaje (magulladuras, caída de fruta),
        /// por lo que sus pérdidas estimadas son las más altas de la configuración.
        /// </summary>
        private void DetectarGranizo(
            LoteInfo lote,
            ThresholdsConfig thresholds,
            ImpactEstimatesConfig impacto,
            DateTime fechaHora,
            double? weatherCode,
            List<EventoClimatico> eventos,
            HashSet<(string, DateTime)> procesados)
            {
            if (weatherCode is not double codigo || codigo < thresholds.GranizoWeatherCodeMin) return;

            var key = ("Granizo", fechaHora.Date);
            if (procesados.Contains(key)) return;

            var severo = codigo >= thresholds.GranizoWeatherCodeSevera;
            var severidad = severo ? "Crítica" : "Alta";
            var perdida = severo ? impacto.GranizoSeveraPerdida : impacto.GranizoPerdida;

            eventos.Add(new EventoClimatico
                {
                LoteId = lote.Id,
                ProductorId = lote.ProductorId,
                Fecha = fechaHora.Date,
                TipoEvento = "Granizo",
                SeveridadEstimada = severidad,
                PorcentajePerdidaEstimada = (decimal?)perdida,
                Fuente = "Detección Automática - OpenMeteo",
                Observaciones = $"weather_code {codigo:0} a las {fechaHora.Hour}:00. Daño mecánico en fruta y follaje."
                });
            procesados.Add(key);
            }
        }

    public class OpenMeteoResponse
        {
        public HourlyData Hourly { get; set; } = new HourlyData();
        }

    public class HourlyData
        {
        public List<DateTime> Time { get; set; } = [];
        public List<double?> Temperature_2m { get; set; } = [];
        public List<double?> Precipitation { get; set; } = [];
        public List<double?> Wind_speed_10m { get; set; } = [];

        /// <summary>
        /// Código meteorológico WMO por hora (0-99). Open-Meteo lo devuelve como entero;
        /// se modela como doble nullable para tolerar ausencia del campo en respuestas viejas.
        /// </summary>
        public List<double?> Weather_code { get; set; } = [];
        }
    }

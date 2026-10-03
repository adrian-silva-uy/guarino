using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GuarinoApp.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GuarinoApp.Infrastructure.Services
    {
    /// <summary>
    /// Envía prompts ya armados al SERVICIO WEB de IA (API compatible con OpenAI)
    /// en régimen gratuito: sin tarjeta, sin costos para la aplicación.
    /// Protecciones del plan free:
    ///  - Concurrencia acotada (ConsultasWebSimultaneas) para no desbordar el
    ///    cupo de consultas por minuto; si el cupo está lleno, espera su turno
    ///    dentro del timeout y si no alcanza devuelve null (manda lo local).
    ///  - Límite de uso (429) o cualquier error → null con log técnico; la
    ///    aplicación sigue con la ruta local o con su cálculo determinista.
    ///  - La clave viaja solo en la cabecera Authorization del servidor y NUNCA
    ///    se registra en logs ni llega al navegador.
    /// Singleton: el candado de consultas simultáneas es único para toda la app.
    /// </summary>
    public class IaWebClient : IIaWebClient
        {
        private const string NombreCliente = "IaWeb";

        private readonly IOptionsMonitor<IaWebOptions> _options;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<IaWebClient> _logger;
        private readonly SemaphoreSlim _candado;

        public IaWebClient(
            IOptionsMonitor<IaWebOptions> options,
            IHttpClientFactory httpClientFactory,
            ILogger<IaWebClient> logger)
            {
            _options = options;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _candado = new SemaphoreSlim(Math.Max(1, options.CurrentValue.ConsultasWebSimultaneas));
            }

        public bool Habilitada
            {
            get
                {
                var opciones = _options.CurrentValue;
                return opciones.Habilitada && !string.IsNullOrWhiteSpace(opciones.ApiKey);
                }
            }

        public async Task<string?> EnviarAsync(
            IReadOnlyList<object> messages,
            bool modoJson,
            CancellationToken cancellationToken = default)
            {
            var opciones = _options.CurrentValue;
            if (!Habilitada || messages == null || messages.Count == 0)
                return null;

            // Cupo por minuto del plan gratuito: si ya hay el máximo de consultas
            // en vuelo, espera su turno (la ruta local corre en paralelo igual).
            if (!await _candado.WaitAsync(TimeSpan.FromSeconds(opciones.TimeoutSegundos), cancellationToken))
                {
                _logger.LogInformation("Servicio web de IA: consultas simultáneas al tope del cupo gratuito; se continúa por la ruta local.");
                return null;
                }

            try
                {
                var cuerpo = new Dictionary<string, object?>
                    {
                    ["model"] = opciones.Modelo,
                    ["messages"] = messages,
                    ["temperature"] = 0.2,
                    ["max_tokens"] = 1400
                    };
                // NOTA: no se envía response_format=json_object: el modelo del plan
                // gratuito (openai/gpt-oss-120b) lo rechaza con 400 y Groq aún no lo
                // soporta para modelos de razonamiento. La exigencia de JSON estricto
                // ya está en todos los prompts ("SOLO JSON válido, sin texto antes ni
                // después"), así que el contrato de salida se mantiene igual.
                _ = modoJson; // parámetro reservado para modelos que lo soporten

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(opciones.TimeoutSegundos));

                var client = _httpClientFactory.CreateClient(NombreCliente);

                using var solicitud = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
                    {
                    Content = JsonContent.Create(cuerpo)
                    };
                solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", opciones.ApiKey);

                var respuesta = await client.SendAsync(solicitud, cts.Token);

                if (!respuesta.IsSuccessStatusCode)
                    {
                    var status = (int)respuesta.StatusCode;
                    if (status == 429)
                        _logger.LogWarning("Servicio web de IA: límite de uso del plan gratuito (429). Se continúa por la ruta local.");
                    else
                        _logger.LogWarning("Servicio web de IA respondió {Status}; se continúa por la ruta local.", status);
                    return null;
                    }

                var json = await respuesta.Content.ReadAsStringAsync(cts.Token);
                using var documento = JsonDocument.Parse(json);

                var contenido = documento.RootElement.TryGetProperty("choices", out var choices)
                    && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0
                    && choices[0].TryGetProperty("message", out var mensaje)
                    && mensaje.TryGetProperty("content", out var content)
                    ? content.GetString()
                    : null;

                if (string.IsNullOrWhiteSpace(contenido))
                    {
                    _logger.LogWarning("Servicio web de IA respondió sin contenido; se continúa por la ruta local.");
                    return null;
                    }

                return contenido;
                }
            catch (OperationCanceledException)
                {
                _logger.LogWarning("Servicio web de IA superó {Segundos}s; se continúa por la ruta local.", opciones.TimeoutSegundos);
                return null;
                }
            catch (Exception ex)
                {
                // Nunca propaga: el llamador siempre conserva su cálculo local.
                _logger.LogWarning(ex, "Servicio web de IA no disponible.");
                return null;
                }
            finally
                {
                _candado.Release();
                }
            }
        }
    }

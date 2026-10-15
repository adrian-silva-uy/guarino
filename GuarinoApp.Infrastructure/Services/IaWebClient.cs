using System.Globalization;
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
                    // 2500: el modelo de razonamiento gasta tokens pensando antes de
                    // responder; con 1400 las consultas largas devolvían content vacío.
                    ["max_tokens"] = 2500,
                    // Razonamiento acotado: mismas respuestas útiles con menos tokens
                    // (menos latencia y menos consumo del cupo por minuto del plan gratis).
                    ["reasoning_effort"] = "low"
                    };
                // NOTA: no se envía response_format=json_object: el modelo del plan
                // gratuito (openai/gpt-oss-120b) lo rechaza con 400 y Groq aún no lo
                // soporta para modelos de razonamiento. La exigencia de JSON estricto
                // ya está en todos los prompts ("SOLO JSON válido, sin texto antes ni
                // después"), así que el contrato de salida se mantiene igual.
                _ = modoJson; // parámetro reservado para modelos que lo soporten

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(opciones.TimeoutSegundos));
                var limite = DateTime.UtcNow.AddSeconds(opciones.TimeoutSegundos);

                // Estimación de los tokens de ESTA petición (≈4 chars/token en el
                // prompt + reserva de salida): con ella se calcula cuánto esperar
                // tras un 429 antes de reintentar.
                var estimadoTokens = JsonSerializer.Serialize(cuerpo).Length / 4 + 400;

                var client = _httpClientFactory.CreateClient(NombreCliente);

                for (var intento = 1; intento <= 2; intento++)
                    {
                    using var solicitud = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
                        {
                        Content = JsonContent.Create(cuerpo)
                        };
                    solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", opciones.ApiKey);

                    var respuesta = await client.SendAsync(solicitud, cts.Token);

                    if (!respuesta.IsSuccessStatusCode)
                        {
                        var status = (int)respuesta.StatusCode;
                        if (status == 429 && intento < 2)
                            {
                            // El 429 del plan gratuito es la ventana por minuto
                            // girando. Se calcula la espera con el propio retry-after
                            // de Groq y con lo que falta para que entre esta petición
                            // (la ventana se rellena a ~133 tok/s al envejecer las
                            // consultas anteriores); así la ráfaga pico de usuarios
                            // se convierte en 200 en lugar de caer al respaldo
                            // determinista. Si no alcanza el timeout total, no se
                            // reintenta y se sigue directo por la ruta local.
                            var espera = CalcularEsperaReintento(respuesta, estimadoTokens, limite - DateTime.UtcNow);
                            if (espera == null)
                                {
                                _logger.LogWarning("Servicio web de IA: cupo por minuto agotado (429) sin tiempo para reintentar; se continúa por la ruta local.");
                                return null;
                                }
                            _logger.LogInformation(
                                "Servicio web de IA: cupo por minuto alcanzado (429); se esperan {Segundos}s y se reintenta.",
                                Math.Round(espera.Value.TotalSeconds, 1));
                            try
                                {
                                await Task.Delay(espera.Value, cts.Token);
                                }
                            catch (OperationCanceledException)
                                {
                                return null;
                                }
                            continue;
                            }

                        if (status == 429)
                            _logger.LogWarning("Servicio web de IA: límite de uso del plan gratuito (429). Se continúa por la ruta local.");
                        else
                            {
                            // El tamaño del cuerpo explica rechazos como el 413
                            // (payload too large): diagnostica prompts que crecieron
                            // demasiado sin exponer su contenido ni la clave.
                            var caracteresCuerpo = JsonSerializer.Serialize(cuerpo).Length;
                            _logger.LogWarning(
                                "Servicio web de IA respondió {Status} (cuerpo de {Caracteres} caracteres); se continúa por la ruta local.",
                                status, caracteresCuerpo);
                            }
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

                return null;
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

        /// <summary>
        /// Calcula cuánto esperar antes de reintentar una consulta rechazada con
        /// 429. Combina (a) el <c>retry-after</c> del servidor, (b) el
        /// <c>x-ratelimit-reset-tokens</c> —instante en que la ventana rueda
        /// completa y la admisión está garantizada, siempre que quepa en el
        /// timeout— y (c) la estimación lineal de lo que falta para que entre
        /// esta petición (la ventana se rellena a ~133 tok/s al envejecer las
        /// consultas anteriores; en ráfagas densas el relleno es más lento, por
        /// eso el reset manda cuando cabe). Devuelve null si el tiempo restante
        /// no alcanza para esperar + reintentar — en ese caso conviene seguir
        /// directo por la ruta local antes que agotar el tiempo del usuario.
        /// </summary>
        private static TimeSpan? CalcularEsperaReintento(HttpResponseMessage respuesta, int estimadoTokens, TimeSpan restante)
            {
            // Margen para que la reintentada alcance a completarse (Groq: 1-4 s).
            var tope = restante.TotalSeconds - 8;
            if (tope < 4)
                return null;

            double espera = 1;

            if (respuesta.Headers.RetryAfter?.Delta is TimeSpan delta)
                espera = Math.Max(espera, delta.TotalSeconds);

            var restantes = 0;
            if (respuesta.Headers.TryGetValues("x-ratelimit-remaining-tokens", out var valores)
                && int.TryParse(valores.FirstOrDefault(), out var disponibles))
                restantes = disponibles;

            double lineal = espera;
            if (estimadoTokens > restantes)
                lineal = Math.Max(lineal, (estimadoTokens - restantes) / 133.0);

            var reset = LeerResetTokens(respuesta);
            if (reset != null && reset <= tope)
                espera = Math.Max(espera, reset.Value);
            else
                espera = Math.Max(espera, lineal);

            if (espera > tope)
                return null;

            return TimeSpan.FromSeconds(Math.Round(Math.Max(espera, 1), 1));
            }

        /// <summary>
        /// Lee <c>x-ratelimit-reset-tokens</c> de la respuesta 429 (formatos
        /// vistos: "6.149s" con coma decimal, "54.472s", "2m52.8s") y lo
        /// devuelve en segundos; null si no está o no se puede leer.
        /// </summary>
        private static double? LeerResetTokens(HttpResponseMessage respuesta)
            {
            if (!respuesta.Headers.TryGetValues("x-ratelimit-reset-tokens", out var valores))
                return null;
            var texto = (valores.FirstOrDefault() ?? string.Empty).Trim();
            if (texto.Length == 0)
                return null;
            try
                {
                if (texto.Contains('m'))
                    {
                    var partes = texto.Split('m');
                    var minutos = double.Parse(partes[0].Replace(',', '.'), CultureInfo.InvariantCulture);
                    var resto = partes.Length > 1
                        ? partes[1].TrimEnd('s').Replace(',', '.')
                        : "0";
                    return minutos * 60 + double.Parse(resto, CultureInfo.InvariantCulture);
                    }
                return double.Parse(texto.TrimEnd('s').Replace(',', '.'), CultureInfo.InvariantCulture);
                }
            catch
                {
                return null;
                }
            }
        }
    }

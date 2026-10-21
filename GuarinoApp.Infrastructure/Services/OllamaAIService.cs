namespace GuarinoApp.Infrastructure.Services;

using GuarinoApp.Application.Common;
using GuarinoApp.Application.Common.Dtos;
using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.Contaduria.Dtos;
using GuarinoApp.Application.Modules.Proyeccion.Dtos;
using GuarinoApp.Application.Modules.Proyeccion.Queries.GetProyeccionZafra;
using GuarinoApp.Application.Settings;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

public class OllamaAIService : IAIService
    {
    private readonly ILogger<OllamaAIService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDashboardDataService _dashboardService;
    private readonly IOptionsMonitor<OllamaOptions> _optionsMonitor;
    private readonly OllamaConnectionState _connectionState;
    private readonly IPromptProvider _promptProvider;
    private readonly IMediator _mediator;
    private readonly ISkillRegistry _skillRegistry;
    private readonly IOptionsMonitor<ClimateRiskSettings> _climateSettings;
    private readonly IMcpDataService _mcpDataService;
    private readonly IIaWebClient _web;
    private readonly IOptionsMonitor<IaWebOptions> _iaWeb;

    // Tope de tiempo propio para la llamada de estrategia de Zafra -- más corto que el timeout
    // general del HttpClient "Ollama" (60s en appsettings.json). Un JSON estructurado de 5-8
    // acciones con un modelo 1B puede tardar mucho o directamente no converger; preferimos
    // fallar rápido a la variante honesta sin IA (FallbackAIService) antes que dejar a la UI
    // "colgada" hasta el timeout completo.
    // 150 s por intento: en esta máquina (5,7 GB de RAM, CPU) cargar el modelo y generar
    // ~700 tokens puede pasar de los 90 s. Corre en segundo plano, no bloquea la pantalla.
    // 330s: esta CPU genera ~4-5 tokens/s, la respuesta completa son ~1000-1300
    // tokens (resumen + benchmark + plan) y la carga en frío del modelo suma
    // ~45-60s. Con timeouts más cortos los intentos se cortaban a mitad de
    // camino, dejaban generaciones huérfanas encoladas y el siguiente intento
    // volvía a expirar — una bola de nieve que impedía completar el ciclo.
    private static readonly TimeSpan ZafraStrategyTimeout = TimeSpan.FromSeconds(330);

    private const string DefaultChatTemplate =
        "Sos un consultor experto en la industria citrícola, plantas empacadoras y exportadoras de fruta a nivel internacional.\n" +
        "DATOS EN TIEMPO REAL DE LA PLANTA (hoy {Date}):\n" +
        "- Stock total: {TotalStock} bins\n" +
        "- Detalle por variedad: {StockByVarietyDetail}\n" +
        "- Crecimiento/variación proyectada: {GrowthPercent}%\n" +
        "- Pallets sin cliente asignado: {PalletsWithoutClient}\n" +
        "- Proyección de la próxima zafra: {ProyeccionZafra}\n\n" +
        "INSTRUCCIONES:\n" +
        "INTENCIÓN PRIMERO: si el usuario solo saluda, hace una prueba técnica o charla sin pedir análisis (ej. \"hola\", \"hola prueba\"),\n" +
        "respondé con un saludo breve y ejemplos de consultas posibles; NO entregues diagnósticos ni datos de la empresa.\n" +
        "Responde de forma DIRECTA, inteligente, analítica y estratégica a la consulta específica del usuario.\n" +
        "Si el usuario solicita recomendaciones o estrategias (ej. impacto operativo, conservación en cámaras, logística de empaque),\n" +
        "proporciona un análisis ejecutivo concreto del sector citrícola incorporando los datos reales disponibles cuando corresponda.\n" +
        "No te limites a repetir números de stock. Usa un tono profesional, claro y estructurado en español.";

    private const string DefaultBusinessSuggestionTemplate =
        "Sos un consultor experto en la industria citrícola, con conocimiento de mejores prácticas de packing houses y exportadoras a nivel internacional. " +
        "Contexto de la empresa en formato JSON: {RequestJson}. " +
        "Parámetros del análisis: confianza mínima {MinConfidence}%, horizonte {HorizonMonths} meses, considerar estacionalidad: {IncludeSeasonality}. " +
        "Devolvé un ÚNICO array JSON con EXACTAMENTE 3 sugerencias estratégicas con formato: [{\"Text\": \"texto\", \"Color\": \"#RRGGBB\", \"Icon\": \"\"}]";

    public OllamaAIService(
        IOptionsMonitor<OllamaOptions> optionsMonitor,
        OllamaConnectionState connectionState,
        ILogger<OllamaAIService> logger,
        IHttpClientFactory httpClientFactory,
        IDashboardDataService dashboardService,
        IPromptProvider promptProvider,
        IMediator mediator,
        ISkillRegistry skillRegistry,
        IOptionsMonitor<ClimateRiskSettings> climateSettings,
        IMcpDataService mcpDataService,
        IIaWebClient web,
        IOptionsMonitor<IaWebOptions> iaWeb)
        {
        _optionsMonitor = optionsMonitor;
        _connectionState = connectionState;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _dashboardService = dashboardService;
        _promptProvider = promptProvider;
        _mediator = mediator;
        _skillRegistry = skillRegistry;
        _climateSettings = climateSettings;
        _mcpDataService = mcpDataService;
        _web = web;
        _iaWeb = iaWeb;
        }

    public bool IsAvailable
        {
        get
            {
            var options = _optionsMonitor.CurrentValue;
            var localLista = options.Enabled
                && !string.IsNullOrWhiteSpace(options.Url)
                && !string.IsNullOrWhiteSpace(options.Model)
                && _connectionState.IsReady;
            // Con el servicio web habilitado la IA sigue disponible aunque la
            // ruta local no responda (la consulta sale en paralelo y el cálculo
            // determinista de cada página queda siempre como respaldo).
            return localLista || _web.Habilitada;
            }
        }

    private HttpClient CreateClient() => _httpClientFactory.CreateClient("Ollama");

    // --- Carrera local ↔ servicio web -----------------------------------------
    // Con picos de hasta ~20 usuarios, la inferencia local va con CONCURRENCIA 1:
    // sin este candado, N generaciones simultáneas multiplican la caché KV por
    // usuario (≈192 MB c/u con num_ctx=6144) y Ollama termina respondiendo 500
    // por falta de memoria. La cola local no frena a nadie: mientras una
    // consulta espera su turno, la contraparte web corre en paralelo.
    private static readonly SemaphoreSlim _ollamaGate = new(1, 1);

    /// <summary>
    /// Si la ruta local está ocupada por otra inferencia, cuánto espera su turno
    /// antes de rendirse y consumir la vía web (que ya está corriendo en paralelo).
    /// Con web deshabilitada se espera el presupuesto completo: lo local es lo único.
    /// </summary>
    private static readonly TimeSpan GraciaColaConWeb = TimeSpan.FromSeconds(15);

    /// <summary>
    /// ¿El contenido devuelto es un plan de zafra parseable (JSON con su lista
    /// de acciones)? Un modelo pequeño a veces devuelve JSON truncado o prosa
    /// disfrazada; eso NO cuenta como "responder" frente a la vía web.
    /// </summary>
    private static bool EsPlanZafraUsable(string contenido)
        {
        if (string.IsNullOrWhiteSpace(contenido))
            return false;

        // Tolera vallas ```json y prosa alrededor: se analiza el trozo { … }.
        var inicio = contenido.IndexOf('{');
        var fin = contenido.LastIndexOf('}');
        if (inicio < 0 || fin <= inicio)
            return false;

        try
            {
            using var doc = JsonDocument.Parse(contenido.Substring(inicio, fin - inicio + 1));
            return doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("accionesEstrategicas", out var acciones)
                && acciones.ValueKind == JsonValueKind.Array
                && acciones.GetArrayLength() > 0;
            }
        catch (System.Text.Json.JsonException)
            {
            return false;
            }
        }

    /// <summary>
    /// Dispara EN PARALELO la consulta local (Ollama) y la consulta web (servicio
    /// externo gratuito) con el mismo prompt ya armado. Prioriza la local: su
    /// respuesta se usa si llega; la web solo se consume cuando la local no
    /// responde (error, vacío, timeout o cola saturada). Si ninguna responde,
    /// devuelve vacío y la página conserva su cálculo determinista — ningún
    /// usuario espera frente a la pantalla.
    /// </summary>
    private async Task<string> EnviarEnParaleloAsync(
        object solicitudOllama,
        List<object> messages,
        bool modoJson,
        TimeSpan? presupuestoLocal,
        string diagnostico,
        Func<string, bool>? contenidoUtil = null,
        CancellationToken cancellationToken = default)
        {
        var ctsWeb = new CancellationTokenSource();
        var tareaWeb = _web.Habilitada
            ? _web.EnviarAsync(messages, modoJson, ctsWeb.Token)
            : Task.FromResult<string?>(null);

        string local;
        try
            {
            local = await EnviarAOllamaAsync(solicitudOllama, presupuestoLocal, diagnostico, cancellationToken);
            }
        catch
            {
            ctsWeb.Cancel();
            try { await tareaWeb; } catch { /* la local se cayó: la web tampoco interesa */ }
            ctsWeb.Dispose();
            throw;
            }

        // "Respondió" solo cuenta si el contenido es usable: un JSON truncado o
        // fuera de forma de la ruta local NO debe ganarle a un plan web válido.
        var localUsable = !string.IsNullOrWhiteSpace(local)
            && (contenidoUtil?.Invoke(local) ?? true);

        if (localUsable)
            {
            // La ruta local respondió: se corta la web en vuelo para no gastar
            // el cupo gratuito en una respuesta que no se va a usar.
            ctsWeb.Cancel();
            try { await tareaWeb; } catch { /* cancelada: no interesa */ }
            ctsWeb.Dispose();
            return local;
            }

        string? remota = null;
        try
            {
            remota = await tareaWeb;
            }
        catch (OperationCanceledException)
            {
            // cancelada por otra vía: se trata como "sin respuesta web".
            }
        finally
            {
            ctsWeb.Dispose();
            }

        if (!string.IsNullOrWhiteSpace(remota) && (contenidoUtil?.Invoke(remota) ?? true))
            {
            _logger.LogInformation(
                "{Diag}: respuesta web consumida ({Motivo}).",
                diagnostico,
                string.IsNullOrWhiteSpace(local) ? "la ruta local no respondió" : "la ruta local respondió contenido no usable");
            return remota;
            }

        // Ni la web ni (con utilidad) lo local: se devuelve lo local aunque esté
        // en prosa para que el método conserve su plan local de siempre.
        return local;
        }

    /// <summary>
    /// Ruta local con CONCURRENCIA 1 y presupuesto de tiempo propio: hasta 2
    /// intentos dentro del presupuesto (misma semántica histórica de reintentos)
    /// — o 1 intento dentro de la ventana <c>EsperaLocalSegundos</c> cuando hay
    /// servicio web habilitado. Si la cola local está saturada y hay servicio
    /// web, cede rápido (15 s) en vez de acumularse; con web deshabilitada
    /// espera su turno completo.
    /// </summary>
    private async Task<string> EnviarAOllamaAsync(
        object solicitudOllama,
        TimeSpan? presupuesto,
        string diagnostico,
        CancellationToken cancellationToken)
        {
        var limite = presupuesto ?? TimeSpan.FromSeconds(300);

        // Con servicio web habilitado, la local tiene una VENTANA para responder
        // (prioridad real, pero acotada): la web ya está corriendo en paralelo y
        // su respuesta queda lista en segundos. Sin web, la local conserva su
        // presupuesto histórico completo porque es lo único que existe.
        if (_web.Habilitada)
            {
            var ventana = TimeSpan.FromSeconds(_iaWeb.CurrentValue.EsperaLocalSegundos);
            if (limite > ventana)
                limite = ventana;
            }

        var esperaCola = _web.Habilitada ? TimeSpan.FromMilliseconds(Math.Min(limite.TotalMilliseconds, GraciaColaConWeb.TotalMilliseconds)) : limite;

        if (!await _ollamaGate.WaitAsync(esperaCola, cancellationToken))
            {
            _logger.LogInformation("{Diag}: la ruta local está ocupada por otra inferencia; se consume la vía web o el cálculo local.", diagnostico);
            return string.Empty;
            }

        try
            {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(limite);
            var client = CreateClient();

            // Con servicio web habilitado basta UN intento local: el reintento
            // histórico existía porque lo local era lo único que había, y ahora
            // solo retrasaría el respaldo web. Sin web, se conservan los 2.
            var maxIntentos = _web.Habilitada ? 1 : 2;

            for (var intento = 1; intento <= maxIntentos; intento++)
                {
                HttpResponseMessage response;
                try
                    {
                    response = await client.PostAsJsonAsync("/api/chat", solicitudOllama, cts.Token);
                    }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                    throw; // cancelación externa (la página/circuito se fue): no es fallo local.
                    }
                catch (OperationCanceledException)
                    {
                    LogAvisoRutaLocal("{Diag}: la ruta local superó {Segundos}s (intento {Intento}/{Intentos}); se consume la vía web o el cálculo local.", diagnostico, limite.TotalSeconds, intento, maxIntentos);
                    return string.Empty;
                    }
                catch (HttpRequestException ex)
                    {
                    LogAvisoRutaLocal("{Diag}: la ruta local no responde ({Error}); se consume la vía web o el cálculo local.", diagnostico, ex.GetType().Name);
                    return string.Empty;
                    }

                if (!response.IsSuccessStatusCode)
                    {
                    LogAvisoRutaLocal("{Diag}: la ruta local devolvió {Status} (intento {Intento}/{Intentos}).", diagnostico, response.StatusCode, intento, maxIntentos);
                    MarcarFalloDeGeneracion($"HTTP {(int)response.StatusCode}");
                    if (intento < maxIntentos)
                        {
                        await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);
                        continue;
                        }

                    return string.Empty;
                    }

                var envelopeJson = await response.Content.ReadAsStringAsync(cts.Token);
                string contenido;
                try
                    {
                    var envelope = JsonSerializer.Deserialize<OllamaChatResponse>(envelopeJson);
                    contenido = envelope?.Message?.Content ?? string.Empty;
                    }
                catch (Exception ex)
                    {
                    LogAvisoRutaLocal(ex, "{Diag}: no se pudo parsear el sobre de respuesta de la ruta local.", diagnostico);
                    return string.Empty;
                    }

                if (!string.IsNullOrWhiteSpace(contenido))
                    return contenido;

                if (intento < maxIntentos)
                    {
                    LogAvisoRutaLocal("{Diag}: la ruta local respondió sin contenido (intento {Intento}/{Intentos}); se reintenta.", diagnostico, intento, maxIntentos);
                    await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);
                    }
                }

            return string.Empty;
            }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
            // El presupuesto local venció (p. ej. durante la espera entre
            // intentos): se rinde la ruta local y se consume la vía web.
            LogAvisoRutaLocal("{Diag}: la ruta local superó {Segundos}s en total; se consume la vía web o el cálculo local.", diagnostico, limite.TotalSeconds);
            return string.Empty;
            }
        finally
            {
            _ollamaGate.Release();
            }
        }

    /// <summary>
    /// Aviso de la ruta local de IA. Con la vía web habilitada, un tropiezo
    /// local (HTTP 5xx, timeout, modelo que no pudo cargarse por falta de RAM
    /// en el equipo) es rutinario: la carrera local↔web ya respondió al usuario
    /// con la vía web y no hay degradación visible, así que va a Information
    /// para no llenar el reporte diario del admin con warnings no accionables.
    /// Sin vía web el tropiezo sí degrada la respuesta → Warning.
    /// </summary>
    private void LogAvisoRutaLocal(string mensaje, params object?[] args)
        {
        if (_web.Habilitada)
            _logger.LogInformation(mensaje, args);
        else
            _logger.LogWarning(mensaje, args);
        }

    /// <summary>Igual que <see cref="LogAvisoRutaLocal(string, object?[])"/>, con la excepción adjunta.</summary>
    private void LogAvisoRutaLocal(Exception ex, string mensaje, params object?[] args)
        {
        if (_web.Habilitada)
            _logger.LogInformation(ex, mensaje, args);
        else
            _logger.LogWarning(ex, mensaje, args);
        }

    private void MarcarFalloDeGeneracion(string motivo)
        {
        if (_connectionState.IsModelReady)
            LogAvisoRutaLocal("Ollama respondió con error al generar ({Motivo}).", motivo);

        _connectionState.IsModelReady = false;

        // Sella el fallo para que OllamaRecuperacionService lo verifique y, si se
        // confirma (HTTP 5xx persistente o sin conexión), repare el servicio solo.
        _connectionState.UltimoFalloEn = DateTime.UtcNow;
        _connectionState.UltimoFalloMotivo = motivo;
        }

    private async Task<string> ObtenerResumenProyeccionZafraAsync()
        {
        try
            {
            var zafras = await _mediator.Send(new GetProyeccionZafraQuery());
            var proyectada = zafras?.FirstOrDefault(z => z.Estado == "Proyectado");

            if (proyectada == null)
                return "sin datos disponibles";

            var signo = proyectada.VariacionPorcentual >= 0 ? "+" : "";
            var etiquetaSimulado = proyectada.EsDummy ? " [dato SIMULADO, sin conexión a planta]" : "";

            return $"{proyectada.Año}: {proyectada.TotalKgs:N0} kg ({signo}{proyectada.VariacionPorcentual:0.0}% vs. año anterior), " +
                   $"meses pico {proyectada.MesesPico}{etiquetaSimulado}";
            }
        catch (Exception ex)
            {
            _logger.LogWarning(ex, "No se pudo obtener la proyección de zafra para el contexto del chat.");
            return "sin datos disponibles";
            }
        }

    public async Task<string> ChatAsync(string prompt, List<ChatMessage> history)
        {
        if (!IsAvailable) return string.Empty;

        try
            {
            var options = _optionsMonitor.CurrentValue;
            var fecha = DateTime.Now;

            var totalStock = await _dashboardService.GetTotalStockAsync(fecha);
            var stockVariedad = await _dashboardService.GetStockByVarietyAsync(fecha);
            var crecimiento = await _dashboardService.GetProjectedGrowthAsync(fecha);
            var palletsSinCliente = await _dashboardService.GetPalletsWithoutClientAsync(fecha);
            var proyeccionZafra = await ObtenerResumenProyeccionZafraAsync();

            var detalleVariedad = stockVariedad is { Count: > 0 }
                ? string.Join(", ", stockVariedad.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} ({kv.Value:N0} bins)"))
                : "sin datos de variedad";

            var template = await LoadPromptTemplateAsync("Chat", DefaultChatTemplate);

            var system = template
                .Replace("{Date}", fecha.ToString("yyyy-MM-dd"))
                .Replace("{TotalStock}", totalStock.ToString("N0"))
                .Replace("{StockByVarietyDetail}", detalleVariedad)
                .Replace("{GrowthPercent}", crecimiento.ToString("0.0"))
                .Replace("{PalletsWithoutClient}", palletsSinCliente.ToString("N0"))
                .Replace("{ProyeccionZafra}", proyeccionZafra);

            // Skills: si la consulta del usuario corresponde a una habilidad
            // registrada, se ejecuta sobre datos reales y el resultado se
            // inyecta como contexto para que la respuesta no invente cifras.
            // MCP: datos externos en vivo (orquestación determinista, ADR-003).
            // Va antes de las skills para que el bloque más volátil quede más
            // cerca del final del system prompt: con num_ctx acotado, Ollama
            // conserva los últimos tokens si hubiera que truncar.
            var mcpContext = await _mcpDataService.GetContextAsync(prompt);
            if (mcpContext.Count > 0)
                {
                system += "\n\nDATOS EXTERNOS EN VIVO (fuentes externas consultadas para esta consulta):\n"
                    + string.Join("\n", mcpContext.Select(x => $"- [Fuente: {x.Server}] {x.Content}"))
                    + "\n\nSi usás estos datos, citá la fuente por su nombre (por ejemplo: según DeepWiki). Si contradicen los datos internos de la planta, priorizá los internos.";
                }
            var skillMatch = await _skillRegistry.MatchAsync(prompt);
            if (skillMatch != null)
                {
                // Rechazo por rol (capa 2 del filtro): el texto ya es la
                // respuesta con el rechazo inteligente — no se consulta el
                // modelo ni se le pasa contexto del sistema.
                if (skillMatch.Restringido)
                    return skillMatch.Text;

                system += skillMatch.Success
                    ? "\n\nDATOS REALES CONSULTADOS AL SISTEMA (habilidad: " + skillMatch.SkillName + "):\n"
                      + skillMatch.Text
                      + "\n\nUsá estos datos reales para responder. No inventes cifras: si un dato no está "
                      + "en esta información, aclará que no está disponible."
                    : "\n\nEl usuario consultó datos del sistema (habilidad: " + skillMatch.SkillName
                      + ") pero no hay información disponible: " + skillMatch.Text
                      + " Aclará en tu respuesta que ese dato todavía no está cargado.";
                }

            var messages = new List<object> { new { role = "system", content = system } };
            foreach (var item in history.TakeLast(10))
                messages.Add(new { role = item.IsUser ? "user" : "assistant", content = item.Text ?? string.Empty });

            messages.Add(new { role = "user", content = prompt });

            // options explícitos: sin num_ctx Ollama usa 2048 y el prompt de
            // chat (plantilla + datos en vivo + MCP + habilidad + historial de
            // 10 turnos) puede excederlo y truncarse. num_predict acota la
            // respuesta para que el turno de la compuerta local se libere antes.
            var request = new
                {
                model = options.Model,
                messages,
                stream = false,
                options = new
                    {
                    num_ctx = 4096,
                    num_predict = 700,
                    temperature = options.Temperature ?? 0.2
                    }
                };

            // Carrera local ↔ web: la local (Ollama) tiene prioridad; si no
            // responde, contesta el servicio web con el mismo prompt armado.
            var reply = await EnviarEnParaleloAsync(request, messages, modoJson: false, presupuestoLocal: null, diagnostico: "Chat");
            if (string.IsNullOrEmpty(reply))
                return string.Empty;

            // AC-1: el pie de la fuente es determinista (el modelo pequeño no
            // siempre obedece la instrucción de citar la fuente dentro del prompt).
            if (reply.Length > 0 && mcpContext.Count > 0)
                reply += "\n\nFuente de datos externos: "
                    + string.Join(", ", mcpContext.Select(x => x.Server).Distinct())
                    + ".";

            return reply;
            }
        catch (Exception ex)
            {
            _logger.LogError(ex, "Error ejecutando ChatAsync en OllamaAIService");
            MarcarFalloDeGeneracion(ex.GetType().Name);
            return string.Empty;
            }
        }

    public async Task<List<BusinessSuggestionDto>> GetBusinessSuggestionsAsync(
        Application.Common.BusinessSuggestionRequest request,
        List<ChatMessage>? history = null)
        {
        var empty = new List<BusinessSuggestionDto>();
        if (!IsAvailable) return empty;

        try
            {
            var options = _optionsMonitor.CurrentValue;
            var template = await LoadPromptTemplateAsync("BusinessSuggestion", DefaultBusinessSuggestionTemplate);

            var reqJson = JsonSerializer.Serialize(request, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var system = template
                .Replace("{RequestJson}", reqJson)
                .Replace("{MinConfidence}", request.MinConfidence.ToString())
                .Replace("{HorizonMonths}", request.HorizonMonths.ToString())
                .Replace("{IncludeSeasonality}", request.IncludeSeasonality ? "sí" : "no");

            var messages = new List<object> { new { role = "system", content = system } };
            if (history != null)
                {
                foreach (var item in history.TakeLast(10))
                    messages.Add(new { role = item.IsUser ? "user" : "assistant", content = item.Text ?? string.Empty });
                }

            messages.Add(new { role = "user", content = "Genera EXACTAMENTE 3 sugerencias estratégicas en JSON según las instrucciones." });

            var requestObj = new
                {
                model = options.Model,
                messages,
                stream = false,
                // Gramática JSON: el modelo solo puede emitir JSON válido (el contrato
                // es un array de 3 sugerencias). num_predict=400: con salidas largas
                // en CPU tardaba minutos; num_ctx=4096 cubre la plantilla corta.
                format = "json",
                options = new { num_predict = 400, temperature = 0.3, num_ctx = 4096 }
                };

            // Timeout duro (120 s) + carrera local ↔ web: mejor volver rápido a
            // las sugerencias locales que dejar al usuario esperando.
            var rawContent = await EnviarEnParaleloAsync(requestObj, messages, modoJson: true, presupuestoLocal: TimeSpan.FromSeconds(120), diagnostico: "GetBusinessSuggestionsAsync");

            // Diagnóstico: ver el inicio del contenido permite detectar prosa frente a JSON.
            var inicio = rawContent.Length > 200 ? rawContent[..200] : rawContent;
            _logger.LogInformation("Sugerencias Ollama: rawContent={Len} chars, IsAvailable={Avail}. Inicio: {Inicio}",
                rawContent?.Length ?? 0, IsAvailable, inicio.Replace('\n', ' ').Replace('\r', ' '));
            if (string.IsNullOrWhiteSpace(rawContent))
                return empty;

            var suggestions = ExtractSuggestionsArray(rawContent);
            _logger.LogInformation("Sugerencias Ollama: {Count} parseadas de rawContent.", suggestions.Count);
            if (suggestions.Any())
                return CompletarHastaTres(suggestions, request);

            var lines = rawContent
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(l => l.Length > 10 && !l.TrimStart().StartsWith("```") && !EsRechazoDelModelo(l) && !EsLineaDeEstructura(l))
                .Take(3)
                .Select(l => new BusinessSuggestionDto { Text = LimpiarLineaDeSugerencia(l), Color = "#9c27b0", Icon = string.Empty })
                .Where(d => !string.IsNullOrWhiteSpace(d.Text) && d.Text.Length > 10)
                .ToList();

            return CompletarHastaTres(lines, request);
            }
        catch (Exception ex)
            {
            _logger.LogError(ex, "Error en GetBusinessSuggestionsAsync");
            MarcarFalloDeGeneracion(ex.GetType().Name);
            return new List<BusinessSuggestionDto>();
            }
        }

    public async Task<ZafraStrategyResultDto> GetZafraInteligentStrategyAsync(
        ZafraDto projectedZafra,
        decimal capacidadMaxHistorica,
        int stockActualBins,
        List<BusinessSuggestionDto>? history = null,
        FinancialDataDto? datosFinancieros = null)
        {
        var empty = new ZafraStrategyResultDto();
        if (!IsAvailable) return empty;

        try
            {
            var options = _optionsMonitor.CurrentValue;
            var template = await LoadPromptTemplateAsync("ZafraStrategy", string.Empty);

            if (string.IsNullOrWhiteSpace(template))
                {
                _logger.LogWarning("No se encontró la plantilla ZafraStrategy.txt.");
                return empty;
                }

            var proyeccionJson = JsonSerializer.Serialize(new
                {
                Anio = projectedZafra.Año,
                TotalKgs = projectedZafra.TotalKgs,
                VariacionPorcentual = projectedZafra.VariacionPorcentual,
                Estado = projectedZafra.Estado,
                EsDummy = projectedZafra.EsDummy,
                BinsEstimados = projectedZafra.BinsEstimados,
                CamionesEstimados = projectedZafra.CamionesEstimados,
                HorasEmpaqueEstimadas = projectedZafra.HorasEmpaqueEstimadas,
                RiesgoCapacidad = projectedZafra.RiesgoCapacidad,
                CapacidadMaximaHistoricaKgs = projectedZafra.CapacidadMaximaHistoricaKgs
                }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            var operationalDataJson = JsonSerializer.Serialize(new
                {
                StockActualBins = stockActualBins,
                CapacidadMaximaHistoricaKgs = capacidadMaxHistorica,
                FechaConsulta = DateTime.Now.ToString("yyyy-MM-dd"),
                CostoPorKgHistorico = (decimal?)datosFinancieros?.CostoPorKg,
                MargenBrutoHistorico = (decimal?)datosFinancieros?.MargenBruto,
                FlujoCajaActual = (decimal?)datosFinancieros?.FlujoCaja,
                CapacidadInstaladaKgs = (decimal?)datosFinancieros?.CapacidadInstalada,
                CostoFleteHistorico = (decimal?)datosFinancieros?.CostoFlete,
                BenchmarkExterno = (string?)null,
                Advertencia = datosFinancieros != null
                    ? "CostoPorKgHistorico, MargenBrutoHistorico, FlujoCajaActual y CapacidadInstaladaKgs " +
                      "son cifras VIGENTES de Contaduría: usalas exactamente como vienen en las fórmulas de " +
                      "capitalNecesario, brechaLiquidez, riesgoLiquidez y oportunidadCAPEX (no las alteres ni " +
                      "las reemplaces por valores típicos de industria). BenchmarkExterno no está configurado: " +
                      "para comparativaBenchmark usá tu conocimiento general de la industria citrícola de " +
                      "exportación con la EXCEPCIÓN CONTROLADA de las instrucciones (etiquetado como no " +
                      "verificado, con rangos y sin cifras puntuales exactas)."
                    : "CostoPorKgHistorico, MargenBrutoHistorico y FlujoCajaActual NO están disponibles " +
                      "(datos específicos de Guarino, no los inventes ni asumas un valor típico de industria): " +
                      "devolvé capitalNecesario=0, brechaLiquidez=0, riesgoLiquidez=\"N/D\" y explicá en " +
                      "comparativaBenchmark que falta el costo por kg y el flujo de caja de Contaduría. " +
                      "BenchmarkExterno tampoco está configurado, pero para comparativaBenchmark SÍ podés usar " +
                      "tu conocimiento general de la industria citrícola de exportación como referencia " +
                      "orientativa -- seguí la EXCEPCIÓN CONTROLADA de las instrucciones."
                }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            var system = template
                .Replace("{ProyeccionZafraData}", proyeccionJson)
                .Replace("{OperationalData}", operationalDataJson);

            var messages = new List<object> { new { role = "system", content = system } };

            if (history != null)
                {
                foreach (var item in history.TakeLast(5))
                    messages.Add(new { role = "user", content = item.Text ?? string.Empty });
                }

            messages.Add(new { role = "user", content = "Genera el plan estratégico en JSON según las instrucciones." });

            var requestObj = new
                {
                model = options.Model,
                messages,
                stream = false,
                // "format": "json" usa la gramática de Ollama: el modelo no puede
                // emitir prosa, markdown ni comentarios — solo JSON válido. Los
                // desajustes de esquema (texto donde pide objeto, números como
                // texto) los absorben los convertidores tolerantes del parser.
                format = "json",
                // num_predict=1400: con 700 el JSON venía truncado a mitad de
                // accionesEstrategicas. num_ctx=6144 (192 MB de caché KV): el default
                // (2048) no alcanza para la plantilla y 8192 (256 MB) exigía memoria
                // que esta máquina no tenía libres; 6144 cubre plantilla (~2,5k
                // tokens) + datos + 1400 de salida con margen.
                options = new { num_predict = 1400, temperature = 0.2, num_ctx = 6144 }
                };

            // Carrera local ↔ web con el MISMO prompt armado: la local corre con
            // concurrencia 1 (protege la RAM) y prioridad —pero acotada a la
            // ventana EsperaLocalSegundos cuando hay web—; si no responde o
            // responde un JSON fuera de forma, se consume la web.
            var rawContent = await EnviarEnParaleloAsync(requestObj, messages, modoJson: true,
                presupuestoLocal: TimeSpan.FromSeconds(ZafraStrategyTimeout.TotalSeconds * 2 + 2),
                diagnostico: "GetZafraInteligentStrategyAsync",
                contenidoUtil: EsPlanZafraUsable);

            if (string.IsNullOrWhiteSpace(rawContent))
                return empty;

            // Diagnóstico: con modelos pequeños la respuesta a veces viene en prosa
            // en lugar de JSON; ver el inicio del contenido permite distinguirlo.
            var inicio = rawContent.Length > 200 ? rawContent[..200] : rawContent;
            _logger.LogInformation("ZafraStrategy: {Len} chars recibidos. Inicio: {Inicio}",
                rawContent.Length, inicio.Replace('\n', ' ').Replace('\r', ' '));

            var parsed = ExtractZafraStrategyResult(rawContent);
            if (parsed != null)
                return parsed;

            var lines = rawContent
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(l => l.Length > 10 && !l.TrimStart().StartsWith("```") && !EsRechazoDelModelo(l) && !EsLineaDeEstructura(l))
                .Take(3)
                .Select(l => new BusinessSuggestionDto { Text = l, Color = "#1976d2", Icon = "strategy", Origen = "Checklist" })
                .ToList();

            return new ZafraStrategyResultDto
                {
                KPIs = null,
                AccionesEstrategicas = lines,
                Escenarios = new List<ScenarioDto>()
                };
            }
        catch (Exception ex)
            {
            _logger.LogError(ex, "Error en GetZafraInteligentStrategyAsync");
            MarcarFalloDeGeneracion(ex.GetType().Name);
            return empty;
            }
        }

    /// <summary>
    /// Análisis de módulo (Contaduría, Gerencia, …) con la misma receta ya validada
    /// en ZafraStrategy: formato JSON con gramática de Ollama (sin prosa posible),
    /// 2 intentos con timeout de 330 s (el primero suele agotarse cargando el modelo
    /// en CPU), saneado de JSON consciente de cadenas y convertidores tolerantes.
    /// Si el parseo falla devuelve EsIA=false y la página conserva su análisis local.
    /// </summary>
    public async Task<ModuloInsightDto> GetAnalisisDeModuloAsync(string clavePrompt, string contextoJson)
        {
        var empty = new ModuloInsightDto();
        if (!IsAvailable || string.IsNullOrWhiteSpace(clavePrompt)) return empty;

        try
            {
            var options = _optionsMonitor.CurrentValue;
            var template = await LoadPromptTemplateAsync(clavePrompt, string.Empty);

            if (string.IsNullOrWhiteSpace(template))
                {
                _logger.LogWarning("No se encontró la plantilla {Clave}.txt para el análisis de módulo.", clavePrompt);
                return empty;
                }

            var system = template.Replace("{Contexto}", contextoJson ?? string.Empty);
            var messages = new List<object>
                {
                new { role = "system", content = system },
                new { role = "user", content = "Genera el análisis en JSON según las instrucciones." }
                };

            var requestObj = new
                {
                model = options.Model,
                messages,
                stream = false,
                // Gramática JSON: el modelo solo puede emitir JSON válido; el contrato
                // es corto (lectura + ≤4 hallazgos + ≤4 acciones) por eso 800 tokens.
                format = "json",
                options = new { num_predict = 1280, temperature = 0.2, num_ctx = 6144 }
                };

            // Carrera local ↔ web con el MISMO prompt armado: prioriza la ruta
            // local (concurrencia 1, presupuesto histórico 2 × 330 s); si no
            // responde, se consume el servicio web.
            var rawContent = await EnviarEnParaleloAsync(requestObj, messages, modoJson: true,
                presupuestoLocal: TimeSpan.FromSeconds(ZafraStrategyTimeout.TotalSeconds * 2 + 2),
                diagnostico: "GetAnalisisDeModuloAsync " + clavePrompt);

            if (string.IsNullOrWhiteSpace(rawContent))
                return empty;

            var inicio = rawContent.Length > 200 ? rawContent[..200] : rawContent;
            _logger.LogInformation("AnalisisModulo {Clave}: {Len} chars recibidos. Inicio: {Inicio}",
                clavePrompt, rawContent.Length, inicio.Replace('\n', ' ').Replace('\r', ' '));

            var parsed = ExtractModuloInsight(rawContent);
            if (parsed == null)
                return empty;

            parsed.EsIA = true;
            return parsed;
            }
        catch (Exception ex)
            {
            _logger.LogError(ex, "Error en GetAnalisisDeModuloAsync ({Clave})", clavePrompt);
            MarcarFalloDeGeneracion(ex.GetType().Name);
            return empty;
            }
        }

    /// <summary>
    /// Parsea la salida del modelo al contrato de análisis de módulo de forma MANUAL
    /// (JsonDocument) porque el modelo pequeño incumple el contrato con relativa
    /// frecuencia: `lectura` a veces viene como objeto de campos etiquetados y las
    /// listas a veces traen strings sueltos. Reglas:
    /// - lectura: solo prosa. Un objeto se acepta únicamente si trae una clave de
    ///   párrafo conocida; si no, se descarta (la página conserva su lectura local)
    ///   para no pintar "etiqueta: valor" concatenado en la tarjeta.
    /// - hallazgos/acciones: aceptan strings sueltos y objetos con claves
    ///   alternativas en inglés/español; severidad normalizada a alta/media/baja.
    /// Devuelve null si no hay contenido aprovechable.
    /// </summary>
    private ModuloInsightDto? ExtractModuloInsight(string raw)
        {
        var cleaned = Regex.Replace(raw, "```(json)?", string.Empty, RegexOptions.IgnoreCase).Trim();
        cleaned = LimpiarJson(cleaned);

        var doc = AbrirJson(cleaned);
        if (doc == null)
            {
            // Respuesta truncada por el límite de tokens: se corta en la última coma
            // completa y se cierran los contenedores pendientes.
            var reparado = RepararJsonTruncado(cleaned);
            doc = reparado != null ? AbrirJson(reparado) : null;
            }
        if (doc == null)
            {
            var intento = Regex.Match(cleaned, @"\{[\s\S]*\}");
            if (intento.Success)
                {
                doc = AbrirJson(intento.Value);
                if (doc == null)
                    {
                    var reparado = RepararJsonTruncado(intento.Value);
                    doc = reparado != null ? AbrirJson(reparado) : null;
                    }
                }
            }
        if (doc == null)
            {
            _logger.LogWarning("No se pudo parsear el JSON del análisis de módulo.");
            return null;
            }

        var dto = new ModuloInsightDto();

        using (doc)
            {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;

            // --- lectura (solo prosa) ---
            if (BuscarPropiedad(root, "lectura", "resumen", "summary") is { } lecturaProp)
                {
                if (lecturaProp.ValueKind == JsonValueKind.String)
                    {
                    dto.Lectura = lecturaProp.GetString() ?? string.Empty;
                    }
                else if (lecturaProp.ValueKind == JsonValueKind.Object)
                    {
                    // El modelo estructuró la lectura en campos: solo se acepta si
                    // trae una clave de párrafo; si no, se descarta (la tarjeta
                    // conserva la lectura local, que ya tiene las mismas cifras).
                    foreach (var nombre in new[] { "texto", "text", "parrafo", "párrafo", "resumen", "contenido", "content", "mensaje", "message" })
                        {
                        var posible = BuscarPropiedad(lecturaProp, nombre);
                        if (posible is { ValueKind: JsonValueKind.String } s && !string.IsNullOrWhiteSpace(s.GetString()))
                            {
                            dto.Lectura = s.GetString() ?? string.Empty;
                            break;
                            }
                        }
                    }
                }

            // --- hallazgos ---
            if (BuscarPropiedad(root, "hallazgos", "findings", "insights") is { ValueKind: JsonValueKind.Array } hallazgos)
                {
                foreach (var item in hallazgos.EnumerateArray())
                    {
                    if (dto.Hallazgos.Count >= 4)
                        break;

                    if (item.ValueKind == JsonValueKind.String)
                        {
                        var texto = item.GetString();
                        if (!string.IsNullOrWhiteSpace(texto))
                            dto.Hallazgos.Add(new InsightHallazgoDto { Titulo = texto.Trim(), Severidad = "media" });
                        }
                    else if (item.ValueKind == JsonValueKind.Object)
                        {
                        var titulo = TextoDe(item, "titulo", "title", "hallazgo", "finding");
                        var detalle = TextoDe(item, "detalle", "detail", "description", "texto", "descripcion");
                        if (string.IsNullOrWhiteSpace(titulo) && string.IsNullOrWhiteSpace(detalle))
                            continue;

                        dto.Hallazgos.Add(new InsightHallazgoDto
                            {
                            Titulo = (titulo ?? string.Empty).Trim(),
                            Detalle = (detalle ?? string.Empty).Trim(),
                            Severidad = SeveridadNormalizada(TextoDe(item, "severidad", "severity", "nivel"))
                            });
                        }
                    }
                }

            // --- acciones ---
            if (BuscarPropiedad(root, "acciones", "actions") is { ValueKind: JsonValueKind.Array } acciones)
                {
                foreach (var item in acciones.EnumerateArray())
                    {
                    if (dto.Acciones.Count >= 4)
                        break;

                    if (item.ValueKind == JsonValueKind.String)
                        {
                        var texto = item.GetString();
                        if (!string.IsNullOrWhiteSpace(texto))
                            dto.Acciones.Add(new InsightAccionDto { Texto = texto.Trim() });
                        }
                    else if (item.ValueKind == JsonValueKind.Object)
                        {
                        var texto = TextoDe(item, "texto", "accion", "action", "descripcion", "detail");
                        if (string.IsNullOrWhiteSpace(texto))
                            continue;

                        dto.Acciones.Add(new InsightAccionDto
                            {
                            Texto = texto.Trim(),
                            Plazo = (TextoDe(item, "plazo", "deadline", "tiempo") ?? string.Empty).Trim(),
                            Impacto = (TextoDe(item, "impacto", "impact", "efecto", "effect", "beneficio") ?? string.Empty).Trim()
                            });
                        }
                    }
                }
            }

        dto.Lectura = (dto.Lectura ?? string.Empty).Trim();

        // El modelo pequeño suele copiar la plantilla del FORMATO ("...cifras del
        // INPUT") o repetir la lista de temas sin redactar. Una lectura con marcas
        // del prompt (INPUT, <...>) o sin ninguna cifra no aporta: se descarta para
        // que la página conserve su lectura local, que sí tiene las cifras.
        if (dto.Lectura.Contains("INPUT", StringComparison.OrdinalIgnoreCase) ||
            dto.Lectura.Contains('<') ||
            !dto.Lectura.Any(char.IsDigit))
            dto.Lectura = string.Empty;

        // Mismo criterio para hallazgos/acciones contaminados con la plantilla, y
        // dedupe: el modelo pequeño a veces repite la misma acción u hallazgo.
        dto.Hallazgos = dto.Hallazgos
            .Where(h => !MarcaDePrompt($"{h.Titulo} {h.Detalle}"))
            .DistinctBy(h => (h.Titulo ?? string.Empty).Trim().ToLowerInvariant())
            .ToList();
        dto.Acciones = dto.Acciones
            .Where(a => !MarcaDePrompt($"{a.Texto} {a.Plazo} {a.Impacto}"))
            .DistinctBy(a => (a.Texto ?? string.Empty).Trim().ToLowerInvariant())
            .ToList();

        // El modelo a veces antepone la letra del índice ("B Brecha de caja"): se
        // limpia para que el título se lea como frase normal.
        foreach (var hallazgo in dto.Hallazgos)
            hallazgo.Titulo = Regex.Replace(hallazgo.Titulo, @"^\p{Lu}\s+(?=\p{L})", string.Empty);

        if (string.IsNullOrWhiteSpace(dto.Lectura) && dto.Hallazgos.Count == 0 && dto.Acciones.Count == 0)
            return null;

        return dto;
        }

    private static bool MarcaDePrompt(string? texto) =>
        !string.IsNullOrWhiteSpace(texto) &&
        (texto.Contains("INPUT", StringComparison.OrdinalIgnoreCase) || texto.Contains('<'));

    private static JsonDocument? AbrirJson(string texto)
        {
        try
            {
            return JsonDocument.Parse(texto);
            }
        catch
            {
            return null;
            }
        }

    /// <summary>
    /// Reconstruye un JSON que el modelo dejó abierto al agotar el límite de tokens:
    /// corta después de la última coma fuera de cadena (todo lo anterior está completo)
    /// y cierra los objetos/arrays que quedaron pendientes. Devuelve null si no hay
    /// corte utilizable — en ese caso se conserva el análisis local.
    /// </summary>
    private static string? RepararJsonTruncado(string json)
        {
        var texto = json.Trim();
        var pila = new Stack<char>();
        var enCadena = false;
        var escapar = false;
        var ultimaComa = -1;
        var cerradoresEnLaComa = string.Empty;

        for (var i = 0; i < texto.Length; i++)
            {
            var c = texto[i];
            if (escapar)
                {
                escapar = false;
                continue;
                }
            if (enCadena)
                {
                if (c == '\\') { escapar = true; continue; }
                if (c == '"') enCadena = false;
                continue;
                }
            if (c == '"') { enCadena = true; continue; }
            if (c == '{') pila.Push('}');
            else if (c == '[') pila.Push(']');
            else if (c == '}' || c == ']')
                {
                if (pila.Count == 0 || pila.Peek() != c) return null;
                pila.Pop();
                }
            else if (c == ',')
                {
                // La enumeración de Stack<T> es LIFO (de adentro hacia afuera),
                // que es exactamente el orden en que hay que cerrar.
                ultimaComa = i;
                cerradoresEnLaComa = string.Concat(pila);
                }
            }

        if (ultimaComa < 0)
            return null;

        var candidato = texto[..ultimaComa].TrimEnd();
        if (candidato.Length == 0)
            return null;

        return candidato + cerradoresEnLaComa;
        }

    private static JsonElement? BuscarPropiedad(JsonElement objeto, params string[] nombres)
        {
        if (objeto.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var propiedad in objeto.EnumerateObject())
            {
            foreach (var nombre in nombres)
                {
                if (string.Equals(propiedad.Name, nombre, StringComparison.OrdinalIgnoreCase))
                    return propiedad.Value;
                }
            }

        return null;
        }

    private static string? TextoDe(JsonElement objeto, params string[] nombres)
        {
        if (BuscarPropiedad(objeto, nombres) is not { } prop)
            return null;

        return prop.ValueKind switch
            {
            JsonValueKind.String => prop.GetString(),
            JsonValueKind.Number => prop.GetRawText(),
            JsonValueKind.True => "sí",
            JsonValueKind.False => "no",
            _ => null
            };
        }

    private static string SeveridadNormalizada(string? severidad)
        {
        var s = (severidad ?? string.Empty).Trim().ToLowerInvariant();
        if (s.StartsWith("alt") || s.Contains("critic")) return "alta";
        if (s.StartsWith("med") || s.Contains("moder")) return "media";
        if (s.StartsWith("baj") || s.Contains("leve")) return "baja";
        return "media";
        }

    /// <summary>
    /// El modelo pequeño a veces devuelve menos objetos de los pedidos. Completa
    /// hasta 3 sugerencias con alternativas deterministas derivadas de los datos
    /// reales del request (mismo contrato Text/Color/Icon de la paleta).
    /// </summary>
    /// <summary>Rechazos típicos del modelo pequeño: no constituyen una sugerencia
    /// ni una estrategia aprovechables (el modelo declina la petición).</summary>
    private static bool EsRechazoDelModelo(string linea) =>
        linea.Contains("no puedo cumplir", StringComparison.OrdinalIgnoreCase)
        || linea.StartsWith("Lo siento", StringComparison.OrdinalIgnoreCase)
        || linea.StartsWith("I'm sorry", StringComparison.OrdinalIgnoreCase);

    /// <summary>Líneas de estructura/markdown del modelo (metadatos como
    /// "* **Color**: Azul (#007bff)"): no son sugerencias accionables.</summary>
    private static bool EsLineaDeEstructura(string linea)
        {
        var t = linea.TrimStart();
        return t.StartsWith("*", StringComparison.Ordinal)
            || t.StartsWith("#", StringComparison.Ordinal)
            || linea.Contains("**", StringComparison.Ordinal);
        }

    /// <summary>Deja el texto accionable de una chip: quita viñetas, el
    /// encabezado "Sugerencia N:" (aunque venga como clave casi-JSON
    /// "Sugerencia1": "..."), comillas/coma final y acorta a 200 caracteres
    /// para que la chip no se convierta en un párrafo.</summary>
    private static string LimpiarLineaDeSugerencia(string linea)
        {
        var t = Regex.Replace(linea, @"^[-*\s]+", string.Empty);
        t = Regex.Replace(t, @"^""?Sugerencia\s*\d+""?\s*:\s*""?", string.Empty, RegexOptions.IgnoreCase);
        t = t.Trim().TrimStart('"').Trim().TrimEnd(',', ';', '"').Trim();
        if (t.Length > 200)
            t = t[..197].TrimEnd() + "…";
        return t;
        }

    private static List<BusinessSuggestionDto> CompletarHastaTres(
        List<BusinessSuggestionDto> sugerencias,
        BusinessSuggestionRequest request)
        {
        if (sugerencias.Count >= 3)
            return sugerencias;

        var respaldo = new List<BusinessSuggestionDto>
            {
            new()
                {
                Text = $"Gestionar {request.PalletsWithoutClient:N0} pallets sin cliente asignado. Liberar espacio con oferta comercial prioritaria.",
                Color = "#ff9800",
                Icon = "Inventory2"
                },
            new()
                {
                Text = $"Stock actual en planta: {request.TotalStock:N0} bins. Verificar compromisos de compra activos contra el plan de {request.PeriodLabel}.",
                Color = "#1976d2",
                Icon = "Warning"
                },
            new()
                {
                Text = $"Crecimiento proyectado: {request.GrowthPercent:0.0}%. Revisar capacidad de empaque y contratación estacional para absorber el incremento.",
                Color = "#388e3c",
                Icon = "TrendingUp"
                }
            };

        foreach (var item in respaldo)
            {
            if (sugerencias.Count >= 3)
                break;
            sugerencias.Add(item);
            }

        return sugerencias;
        }
    private List<BusinessSuggestionDto> ExtractSuggestionsArray(string raw)
        {
        var result = new List<BusinessSuggestionDto>();
        var cleaned = Regex.Replace(raw, "```(json)?", string.Empty, RegexOptions.IgnoreCase).Trim();

        foreach (Match match in Regex.Matches(cleaned, @"\[[\s\S]*?\]"))
            {
            try
                {
                var parsed = JsonSerializer.Deserialize<List<BusinessSuggestionDto>>(
                    match.Value, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (parsed != null)
                    result.AddRange(parsed.Where(s => !string.IsNullOrWhiteSpace(s.Text)));
                }
            catch (Exception ex)
                {
                _logger.LogWarning(ex, "No se pudo parsear fragmento JSON de Ollama.");
                }
            }

        // El modelo a veces devuelve un objeto único en vez de array: se acepta como
        // primera sugerencia (CompletarHastaTres completa las otras dos). Sin este
        // intento, la respuesta entera se perdía ("0 parseadas") y se caía a prosa.
        if (result.Count == 0)
            {
            var objeto = Regex.Match(cleaned, @"\{[\s\S]*\}");
            if (objeto.Success)
                {
                try
                    {
                    var opciones = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    opciones.Converters.Add(new LenientStringConverter());
                    var unica = JsonSerializer.Deserialize<BusinessSuggestionDto>(objeto.Value, opciones);
                    if (unica != null && !string.IsNullOrWhiteSpace(unica.Text))
                        {
                        // Un icono ilegible (p. ej. "??") no aporta: la UI ya sabe
                        // renderizar el estado sin icono.
                        if (unica.Icon != null && !unica.Icon.Any(char.IsLetter))
                            unica.Icon = string.Empty;
                        result.Add(unica);
                        }
                    }
                catch (Exception ex)
                    {
                    _logger.LogWarning(ex, "No se pudo parsear objeto único de Ollama como sugerencia.");
                    }
                }
            }

        return result;
        }

    private ZafraStrategyResultDto? ExtractZafraStrategyResult(string raw)
        {
        var cleaned = Regex.Replace(raw, "```(json)?", string.Empty, RegexOptions.IgnoreCase).Trim();
        cleaned = LimpiarJson(cleaned);
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        jsonOptions.Converters.Add(new LenientStringConverter());
        jsonOptions.Converters.Add(new LenientNullableDecimalConverter());

        var directo = TryDeserializeZafraStrategy(cleaned, jsonOptions);
        if (directo != null)
            return MapToResultDto(directo);

        var match = Regex.Match(cleaned, @"\{[\s\S]*\}");
        if (match.Success)
            {
            var fragmento = TryDeserializeZafraStrategy(match.Value, jsonOptions);
            if (fragmento != null)
                return MapToResultDto(fragmento);
            }

        // Diagnóstico: con `format:json` Ollama garantiza sintaxis válida, así que si
        // llegamos acá el problema es de esquema. Ver el final del contenido permite
        // verlo sin reproducciones manuales.
        var fin = cleaned.Length > 300 ? cleaned[^300..] : cleaned;
        _logger.LogWarning("ZafraStrategy: JSON no parseable ({Len} chars). Fin del contenido: {Fin}",
            cleaned.Length, fin.Replace('\n', ' ').Replace('\r', ' '));

        return null;
        }

    /// <summary>
    /// Sanea la salida del modelo antes de parsearla: retira comentarios (// y /* */),
    /// comas finales antes de } o ] y todo lo demás intacto. El recorrido es consciente
    /// de las cadenas, así que una URL o una ruta dentro de un string no se toca.
    /// </summary>
    private static string LimpiarJson(string json)
        {
        var sb = new StringBuilder(json.Length);
        var enCadena = false;
        var escapar = false;

        for (var i = 0; i < json.Length; i++)
            {
            var c = json[i];

            if (enCadena)
                {
                sb.Append(c);
                if (escapar)
                    escapar = false;
                else if (c == '\\')
                    escapar = true;
                else if (c == '"')
                    enCadena = false;
                continue;
                }

            if (c == '"')
                {
                enCadena = true;
                sb.Append(c);
                continue;
                }

            // Comentario de línea: se salta hasta el salto de línea (se conserva).
            if (c == '/' && i + 1 < json.Length && json[i + 1] == '/')
                {
                while (i < json.Length && json[i] != '\n' && json[i] != '\r')
                    i++;
                if (i < json.Length)
                    sb.Append(json[i]);
                continue;
                }

            // Comentario de bloque: se salta hasta */.
            if (c == '/' && i + 1 < json.Length && json[i + 1] == '*')
                {
                i += 2;
                while (i + 1 < json.Length && !(json[i] == '*' && json[i + 1] == '/'))
                    i++;
                i++;
                continue;
                }

            // Coma final antes de cerrar objeto o arreglo: se omite.
            if (c == ',')
                {
                var j = i + 1;
                while (j < json.Length && (json[j] == ' ' || json[j] == '\t' || json[j] == '\n' || json[j] == '\r'))
                    j++;
                if (j < json.Length && (json[j] == '}' || json[j] == ']'))
                    continue;
                }

            sb.Append(c);
            }

        return sb.ToString();
        }

    private ZafraStrategyAiResponseDto? TryDeserializeZafraStrategy(string json, JsonSerializerOptions options)
        {
        try
            {
            var parsed = JsonSerializer.Deserialize<ZafraStrategyAiResponseDto>(json, options);
            if (parsed == null || (parsed.Kpis == null && (parsed.AccionesEstrategicas == null || parsed.AccionesEstrategicas.Count == 0)))
                return null;

            return parsed;
            }
        catch (Exception ex)
            {
            _logger.LogWarning(ex, "No se pudo parsear el objeto JSON de ZafraStrategy.");
            return null;
            }
        }

    private static ZafraStrategyResultDto MapToResultDto(ZafraStrategyAiResponseDto ai)
        {
        FinancialKpiDto? kpis = null;
        if (ai.Kpis != null)
            {
            kpis = new FinancialKpiDto
                {
                CapitalNecesario = ai.Kpis.CapitalNecesario ?? 0m,
                BrechaLiquidez = ai.Kpis.BrechaLiquidez ?? 0m,
                RiesgoLiquidez = string.IsNullOrWhiteSpace(ai.Kpis.RiesgoLiquidez) ? "N/D" : ai.Kpis.RiesgoLiquidez,
                OportunidadCAPEX = string.IsNullOrWhiteSpace(ai.Kpis.OportunidadCapex) ? "N/D" : ai.Kpis.OportunidadCapex,
                ComparativaBenchmark = ai.Kpis.ComparativaBenchmark ?? string.Empty
                };
            }

        var acciones = (ai.AccionesEstrategicas ?? new List<ZafraStrategyAiAccionDto>())
            .Where(a => !string.IsNullOrWhiteSpace(a.Texto) && !EsTextoDeEjemplo(a.Texto))
            .Select(a => new BusinessSuggestionDto
                {
                Text = a.Texto?.Trim() ?? string.Empty,
                Color = string.IsNullOrWhiteSpace(a.Color) ? "#607D8B" : a.Color,
                Icon = string.IsNullOrWhiteSpace(a.Icon) ? "info" : a.Icon,
                Categoria = SaneareCategoria(a.Categoria),
                Origen = string.IsNullOrWhiteSpace(a.Origen) ? "Checklist" : a.Origen.Split('|')[0].Trim(),
                Impacto = ContieneTextoDeEjemplo(a.Impacto) ? null : a.Impacto,
                Riesgo = ContieneTextoDeEjemplo(a.Riesgo) ? null : a.Riesgo
                })
            .GroupBy(a => a.Text, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        // Resumen y benchmark: si el modelo repitió el EJEMPLO del prompt se descartan
        // (la página conserva su texto local) — nunca texto de plantilla en pantalla.
        var resumen = ai.ResumenEjecutivo ?? string.Empty;
        if (ContieneTextoDeEjemplo(resumen))
            resumen = string.Empty;
        if (kpis != null && ContieneTextoDeEjemplo(kpis.ComparativaBenchmark))
            kpis.ComparativaBenchmark = string.Empty;

        return new ZafraStrategyResultDto
            {
            KPIs = kpis,
            AccionesEstrategicas = acciones,
            Escenarios = new List<ScenarioDto>(),
            ResumenEjecutivo = resumen,
            EsIA = true
            };
        }

    /// <summary>
    /// Los modelos chicos a veces repiten textualmente el EJEMPLO del prompt
    /// ("Verbo + decisión concreta, sin relleno" / "Cifra concreta en USD o kg…").
    /// Un plan con textos de plantilla arruina el tablero y el PDF, así que se
    /// filtra acá, antes de que la página adopte la respuesta: si no queda ninguna
    /// acción válida, la página conserva su plan local determinista.
    /// </summary>
    private static bool EsTextoDeEjemplo(string texto)
        {
        var t = texto.Trim();
        return t.Length < 10
            || t.Contains("Verbo + decisión", StringComparison.OrdinalIgnoreCase)
            || t.Contains("sin relleno", StringComparison.OrdinalIgnoreCase)
            || t.Contains("lorem ipsum", StringComparison.OrdinalIgnoreCase)
            || t.Contains("Cifra concreta en USD", StringComparison.OrdinalIgnoreCase)
            || t.Contains("Riesgo o trade-off de tomar", StringComparison.OrdinalIgnoreCase)
            || t.Contains("Veredicto de 3-4 oraciones", StringComparison.OrdinalIgnoreCase)
            || t.Contains("Veredicto con cifras del INPUT", StringComparison.OrdinalIgnoreCase);
        }

    private static bool ContieneTextoDeEjemplo(string? texto)
        => !string.IsNullOrWhiteSpace(texto) && EsTextoDeEjemplo(texto);

    /// <summary>
    /// El ejemplo del prompt mostraba la categoria como lista "Financiero | Calidad | …"
    /// y el modelo chico la copia tal cual; el tablero agrupa por UN eje, así que se
    /// queda el primer token. Sin categoría válida devuelve null (la acción conserva
    /// su tarjeta individual en la página).
    /// </summary>
    private static string? SaneareCategoria(string? categoria)
        {
        if (string.IsNullOrWhiteSpace(categoria))
            return categoria;
        var primerToken = categoria
            .Split(new[] { '|', '/', ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .FirstOrDefault(p => p.Length > 0);
        return string.IsNullOrWhiteSpace(primerToken) ? null : primerToken;
        }

    private async Task<string> LoadPromptTemplateAsync(string key, string embeddedFallback)
        {
        try
            {
            var template = await _promptProvider.GetPromptAsync(key);
            if (!string.IsNullOrWhiteSpace(template))
                return template;
            }
        catch (Exception ex)
            {
            _logger.LogWarning(ex, "No se pudo leer la plantilla '{Key}'; se usa la plantilla embebida.", key);
            }

        return embeddedFallback;
        }

    // ==========================================
    // NUEVO MÉTODO: Gestión de Riesgo Climático (CORREGIDO)
    // ==========================================
    public async Task<string> GetClimateRiskStrategyAsync(string inputJson, CancellationToken cancellationToken)
        {
        if (!IsAvailable)
            {
            _logger.LogWarning("Servicio Ollama no disponible para estrategia climática.");
            return string.Empty; // sin IA: el módulo aplica su respaldo marcado "sin IA"
            }

        try
            {
            var options = _optionsMonitor.CurrentValue;
            var settings = _climateSettings.CurrentValue; // Obtener configuración de clima

            // 1. Cargar el prompt base
            var template = await LoadPromptTemplateAsync("ClimateRiskStrategy", string.Empty);
            if (string.IsNullOrWhiteSpace(template))
                {
                _logger.LogWarning("No se encontró la plantilla ClimateRiskStrategy.txt. Sin plan IA.");
                return string.Empty;
                }

            // 2. Inyectar valores de configuración en el prompt
            var prompt = template
                .Replace("{REGIONAL_CONTEXT}", settings.RegionalContext)
                .Replace("{HELA_SEVERA_TEMP}", settings.Thresholds.HeladaTempSevera.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
                .Replace("{HELA_TEMP}", settings.Thresholds.HeladaTempMin.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
                .Replace("{LLUVIA_EXCESIVA_MM}", settings.Thresholds.LluviaExcesivaMm.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
                .Replace("{VIENTO_FUERTE_KMH}", settings.Thresholds.VientoFuerteKmh.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
                .Replace("{CALOR_EXTREMO_TEMP}", settings.Thresholds.CalorExtremoTempMax.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
                .Replace("{LLUVIA_MUY_EXCESIVA_MM}", settings.Thresholds.LluviaMuyExcesivaMm.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
                .Replace("{VIENTO_FUERTE_KMH_CRITICO}", settings.Thresholds.VientoFuerteKmhCritico.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
                .Replace("{GRANIZO_CODE_MIN}", settings.Thresholds.GranizoWeatherCodeMin.ToString("F0", System.Globalization.CultureInfo.InvariantCulture))
                .Replace("{GRANIZO_CODE_SEVERA}", settings.Thresholds.GranizoWeatherCodeSevera.ToString("F0", System.Globalization.CultureInfo.InvariantCulture))
                .Replace("{HELA_SEVERA_PERDIDA}", settings.ImpactEstimates.HeladaSeveraPerdida.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
                .Replace("{HELA_PERDIDA}", settings.ImpactEstimates.HeladaPerdida.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
                .Replace("{LLUVIA_MUY_EXCESIVA_PERDIDA}", settings.ImpactEstimates.LluviaMuyExcesivaPerdida.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
                .Replace("{LLUVIA_EXCESIVA_PERDIDA}", settings.ImpactEstimates.LluviaExcesivaPerdida.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
                .Replace("{VIENTO_FUERTE_PERDIDA}", settings.ImpactEstimates.VientoFuertePerdida.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
                .Replace("{CALOR_EXTREMO_PERDIDA}", settings.ImpactEstimates.CalorExtremoPerdida.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
                .Replace("{GRANIZO_PERDIDA}", settings.ImpactEstimates.GranizoPerdida.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
                .Replace("{GRANIZO_SEVERA_PERDIDA}", settings.ImpactEstimates.GranizoSeveraPerdida.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
                .Replace("{INPUT_JSON}", inputJson);

            // 3. Construir la solicitud a Ollama
            var messages = new List<object> { new { role = "system", content = prompt } };
            messages.Add(new { role = "user", content = "Genera el plan de mitigación en JSON estricto." });

            // OPTIMIZACIÓN CRÍTICA:
            // - num_ctx: acotado a 4096 (16384 provoca HTTP 500 en Ollama en esta
            //   máquina): este prompt puede excederlo y la ruta local responder
            //   sobre un prompt incompleto — por eso la carrera prioriza la vía
            //   web, que recibe el prompt completo y es la que resuelve.
            // - num_predict: el plan (4 a 8 acciones con impacto y riesgo) puede
            //   superar los 600 tokens y quedar como JSON truncado; se acota igual
            //   para que la generación no se dispare.
            // - keep_alive: evita que Ollama descargue el modelo entre peticiones
            var requestObj = new
                {
                model = options.Model,
                messages,
                stream = false,
                options = new
                    {
                    num_ctx = Math.Max(options.NumCtx ?? 2048, 4096),
                    num_predict = 1024,
                    temperature = options.Temperature ?? 0.2,
                    top_k = options.TopK ?? 40,
                    top_p = options.TopP ?? 0.9
                    },
                keep_alive = string.IsNullOrWhiteSpace(options.KeepAlive) ? "30m" : options.KeepAlive
                };

            // Carrera local ↔ web con el MISMO prompt armado; solo cuenta contenido
            // JSON utilizable (la prosa del modelo pequeño no debe ganarle a una
            // respuesta web válida) y, si nadie responde, vacío = "sin IA".
            var contenido = await EnviarEnParaleloAsync(requestObj, messages, modoJson: true,
                presupuestoLocal: null, diagnostico: "GetClimateRiskStrategyAsync",
                contenidoUtil: EsJsonUtilizable, cancellationToken: cancellationToken);

            // Devolvemos el JSON limpio para que ClimateRiskService lo valide.
            var rawContent = StripJsonFences(contenido ?? string.Empty);
            return EsJsonUtilizable(rawContent) ? rawContent : string.Empty;
            }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
            // Es un Timeout de HttpClient, NO una caída del servidor.
            // No marcamos IsModelReady = false para no apagar toda la IA.
            _logger.LogError(ex, "Timeout de Ollama en GetClimateRiskStrategyAsync tras esperar el tiempo configurado.");
            MarcarFalloDeGeneracion("Timeout", esFalloDeServidor: false);
            return string.Empty;
            }
        catch (Exception ex)
            {
            _logger.LogError(ex, "Error inesperado en GetClimateRiskStrategyAsync");
            MarcarFalloDeGeneracion(ex.GetType().Name, esFalloDeServidor: true);
            return string.Empty;
            }
        }

    // ==========================================
    // MÉTODO AUXILIAR: Limpiar code fences de JSON
    // ==========================================
    private static string StripJsonFences(string raw)
        {
        if (string.IsNullOrWhiteSpace(raw))
            return raw;

        var cleaned = Regex.Replace(raw, @"```(json)?", string.Empty, RegexOptions.IgnoreCase).Trim();
        return cleaned;
        }

    /// <summary>
    /// ¿El contenido devuelto es JSON parseable? Un modelo pequeño a veces devuelve
    /// prosa o JSON truncado: eso NO debe ganarle a una respuesta web válida en la
    /// carrera local ↔ web, ni llegar a ClimateRiskService como si fuera el plan.
    /// </summary>
    private static bool EsJsonUtilizable(string contenido)
        {
        if (string.IsNullOrWhiteSpace(contenido))
            return false;

        try
            {
            using var _ = JsonDocument.Parse(StripJsonFences(contenido));
            return true;
            }
        catch (JsonException)
            {
            return false;
            }
        }

    // ==========================================
    // MÉTODO: Marcar fallo de generación
    // ==========================================
    private void MarcarFalloDeGeneracion(string motivo, bool esFalloDeServidor = true)
        {
        if (_connectionState.IsModelReady)
            LogAvisoRutaLocal("Ollama respondió con error al generar ({Motivo}).", motivo);

        // Solo marcamos el modelo como no listo si es un fallo real del servidor (HTTP error, network down).
        // Un timeout o una respuesta vacía no significa que el modelo esté roto, solo que esa petición falló.
        if (esFalloDeServidor)
            {
            _connectionState.IsModelReady = false;

            // Sella el fallo para que OllamaRecuperacionService lo verifique y, si
            // se confirma (HTTP 5xx persistente o sin conexión), repare solo.
            _connectionState.UltimoFalloEn = DateTime.UtcNow;
            _connectionState.UltimoFalloMotivo = motivo;
            }
        }
    }

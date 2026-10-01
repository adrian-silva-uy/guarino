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
using System.Text.Json;
using System.Text.Json.Serialization;
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

    // Tope de tiempo propio para la llamada de estrategia de Zafra -- más corto que el timeout
    // general del HttpClient "Ollama" (60s en appsettings.json). Un JSON estructurado de 5-8
    // acciones con un modelo 1B puede tardar mucho o directamente no converger; preferimos
    // fallar rápido a la variante honesta sin IA (FallbackAIService) antes que dejar a la UI
    // "colgada" hasta el timeout completo.
    private static readonly TimeSpan ZafraStrategyTimeout = TimeSpan.FromSeconds(25);

    private const string DefaultChatTemplate =
        "Sos un consultor experto en la industria citrícola, plantas empacadoras y exportadoras de fruta a nivel internacional.\n" +
        "DATOS EN TIEMPO REAL DE LA PLANTA (hoy {Date}):\n" +
        "- Stock total: {TotalStock} bins\n" +
        "- Detalle por variedad: {StockByVarietyDetail}\n" +
        "- Crecimiento/variación proyectada: {GrowthPercent}%\n" +
        "- Pallets sin cliente asignado: {PalletsWithoutClient}\n" +
        "- Proyección de la próxima zafra: {ProyeccionZafra}\n\n" +
        "INSTRUCCIONES:\n" +
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
        IMcpDataService mcpDataService)
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
        }

    public bool IsAvailable
        {
        get
            {
            var options = _optionsMonitor.CurrentValue;
            return options.Enabled
                && !string.IsNullOrWhiteSpace(options.Url)
                && !string.IsNullOrWhiteSpace(options.Model)
                && _connectionState.IsReady;
            }
        }

    private HttpClient CreateClient() => _httpClientFactory.CreateClient("Ollama");

    private void MarcarFalloDeGeneracion(string motivo)
        {
        if (_connectionState.IsModelReady)
            _logger.LogWarning("Ollama respondió con error al generar ({Motivo}).", motivo);

        _connectionState.IsModelReady = false;
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

            var request = new { model = options.Model, messages, stream = false };

            var client = CreateClient();
            var response = await client.PostAsJsonAsync("/api/chat", request);
            if (!response.IsSuccessStatusCode)
                {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Ollama Chat error: {Status} - {Error}", response.StatusCode, error);
                MarcarFalloDeGeneracion($"HTTP {(int)response.StatusCode}");
                return string.Empty;
                }

            var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>();
            var reply = result?.Message?.Content ?? string.Empty;

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

            var requestObj = new { model = options.Model, messages, stream = false };

            var client = CreateClient();
            var response = await client.PostAsJsonAsync("/api/chat", requestObj);
            if (!response.IsSuccessStatusCode)
                {
                _logger.LogWarning("Ollama GetBusinessSuggestionsAsync error: {Status}", response.StatusCode);
                MarcarFalloDeGeneracion($"HTTP {(int)response.StatusCode}");
                return empty;
                }

            var envelopeJson = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Sugerencias Ollama HTTP {Status}, respuesta ({Len} chars).", response.StatusCode, envelopeJson.Length);
            string rawContent;
            try
                {
                var envelope = JsonSerializer.Deserialize<OllamaChatResponse>(envelopeJson);
                rawContent = envelope?.Message?.Content ?? string.Empty;
                }
            catch (Exception ex)
                {
                _logger.LogWarning(ex, "No se pudo parsear el sobre de respuesta de Ollama.");
                return empty;
                }

            _logger.LogInformation("Sugerencias Ollama: rawContent={Len} chars, IsAvailable={Avail}.", rawContent?.Length ?? 0, IsAvailable);
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
                options = new { num_predict = 1000, temperature = 0.2 }
                };

            using var cts = new CancellationTokenSource(ZafraStrategyTimeout);

            var client = CreateClient();
            HttpResponseMessage response;
            try
                {
                response = await client.PostAsJsonAsync("/api/chat", requestObj, cts.Token);
                }
            catch (OperationCanceledException)
                {
                _logger.LogWarning("Ollama GetZafraInteligentStrategyAsync superó el tiempo límite de {Segundos}s; se usa el fallback sin IA.", ZafraStrategyTimeout.TotalSeconds);
                return empty;
                }

            if (!response.IsSuccessStatusCode)
                {
                _logger.LogWarning("Ollama GetZafraInteligentStrategyAsync error: {Status}", response.StatusCode);
                MarcarFalloDeGeneracion($"HTTP {(int)response.StatusCode}");
                return empty;
                }

            var envelopeJson = await response.Content.ReadAsStringAsync();
            string rawContent;
            try
                {
                var envelope = JsonSerializer.Deserialize<OllamaChatResponse>(envelopeJson);
                rawContent = envelope?.Message?.Content ?? string.Empty;
                }
            catch (Exception ex)
                {
                _logger.LogWarning(ex, "No se pudo parsear el sobre de respuesta de Ollama para ZafraStrategy.");
                return empty;
                }

            if (string.IsNullOrWhiteSpace(rawContent))
                return empty;

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

        return result;
        }

    private ZafraStrategyResultDto? ExtractZafraStrategyResult(string raw)
        {
        var cleaned = Regex.Replace(raw, "```(json)?", string.Empty, RegexOptions.IgnoreCase).Trim();
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

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

        return null;
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
            .Where(a => !string.IsNullOrWhiteSpace(a.Texto))
            .Select(a => new BusinessSuggestionDto
                {
                Text = a.Texto,
                Color = string.IsNullOrWhiteSpace(a.Color) ? "#607D8B" : a.Color,
                Icon = string.IsNullOrWhiteSpace(a.Icon) ? "info" : a.Icon,
                Categoria = a.Categoria,
                Origen = string.IsNullOrWhiteSpace(a.Origen) ? "Checklist" : a.Origen,
                Impacto = a.Impacto,
                Riesgo = a.Riesgo
                })
            .ToList();

        return new ZafraStrategyResultDto
            {
            KPIs = kpis,
            AccionesEstrategicas = acciones,
            Escenarios = new List<ScenarioDto>()
            };
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

    private class ZafraStrategyAiResponseDto
        {
        public ZafraStrategyAiKpisDto? Kpis { get; set; }
        public List<ZafraStrategyAiAccionDto>? AccionesEstrategicas { get; set; }
        }

    private class ZafraStrategyAiKpisDto
        {
        public decimal? CapitalNecesario { get; set; }
        public decimal? BrechaLiquidez { get; set; }
        public string? RiesgoLiquidez { get; set; }
        public string? OportunidadCapex { get; set; }
        public string? ComparativaBenchmark { get; set; }
        }

    private class ZafraStrategyAiAccionDto
        {
        public string? Texto { get; set; }
        public string? Categoria { get; set; }
        public string? Origen { get; set; }
        public string? Color { get; set; }
        public string? Icon { get; set; }
        public string? Impacto { get; set; }
        public string? Riesgo { get; set; }
        }

    // ==========================================
    // NUEVO MÉTODO: Gestión de Riesgo Climático (CORREGIDO)
    // ==========================================
    public async Task<string> GetClimateRiskStrategyAsync(string inputJson, CancellationToken cancellationToken)
        {
        if (!IsAvailable)
            {
            _logger.LogWarning("Servicio Ollama no disponible para estrategia climática.");
            return GetFallbackResponse(inputJson); // Fallback si no hay IA
            }

        try
            {
            var options = _optionsMonitor.CurrentValue;
            var settings = _climateSettings.CurrentValue; // Obtener configuración de clima

            // 1. Cargar el prompt base
            var template = await LoadPromptTemplateAsync("ClimateRiskStrategy", string.Empty);
            if (string.IsNullOrWhiteSpace(template))
                {
                _logger.LogWarning("No se encontró la plantilla ClimateRiskStrategy.txt. Usando fallback.");
                return GetFallbackResponse(inputJson);
                }

            // 2. Inyectar valores de configuración en el prompt
            var prompt = template
                .Replace("{REGIONAL_CONTEXT}", settings.RegionalContext)
                .Replace("{HELA_SEVERA_TEMP}", settings.Thresholds.HeladaTempSevera.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
                .Replace("{HELA_TEMP}", settings.Thresholds.HeladaTempMin.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
                .Replace("{LLUVIA_EXCESIVA_MM}", settings.Thresholds.LluviaExcesivaMm.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
                .Replace("{VIENTO_FUERTE_KMH}", settings.Thresholds.VientoFuerteKmh.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
                .Replace("{CALOR_EXTREMO_TEMP}", settings.Thresholds.CalorExtremoTempMax.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
                .Replace("{HELA_SEVERA_PERDIDA}", settings.ImpactEstimates.HeladaSeveraPerdida.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
                .Replace("{HELA_PERDIDA}", settings.ImpactEstimates.HeladaPerdida.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
                .Replace("{LLUVIA_MUY_EXCESIVA_PERDIDA}", settings.ImpactEstimates.LluviaMuyExcesivaPerdida.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
                .Replace("{LLUVIA_EXCESIVA_PERDIDA}", settings.ImpactEstimates.LluviaExcesivaPerdida.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
                .Replace("{VIENTO_FUERTE_PERDIDA}", settings.ImpactEstimates.VientoFuertePerdida.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
                .Replace("{CALOR_EXTREMO_PERDIDA}", settings.ImpactEstimates.CalorExtremoPerdida.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
                .Replace("{INPUT_JSON}", inputJson);

            // 3. Construir la solicitud a Ollama
            var messages = new List<object> { new { role = "system", content = prompt } };
            messages.Add(new { role = "user", content = "Genera el plan de mitigación en JSON estricto." });

            // OPTIMIZACIÓN CRÍTICA:
            // - num_predict: limita tokens de salida para evitar timeout en generación infinita
            // - num_ctx: aumenta contexto para no truncar el prompt
            // - keep_alive: evita que Ollama descargue el modelo entre peticiones
            var requestObj = new
                {
                model = options.Model,
                messages,
                stream = false,
                options = new
                    {
                    num_ctx = Math.Max(options.NumCtx ?? 2048, 4096),
                    num_predict = 600,
                    temperature = options.Temperature ?? 0.2,
                    top_k = options.TopK ?? 40,
                    top_p = options.TopP ?? 0.9
                    },
                keep_alive = string.IsNullOrWhiteSpace(options.KeepAlive) ? "30m" : options.KeepAlive
                };

            var client = CreateClient();
            var response = await client.PostAsJsonAsync("/api/chat", requestObj, cancellationToken);

            if (!response.IsSuccessStatusCode)
                {
                _logger.LogWarning("Ollama GetClimateRiskStrategyAsync error: {Status}", response.StatusCode);
                MarcarFalloDeGeneracion($"HTTP {(int)response.StatusCode}", esFalloDeServidor: true);
                return GetFallbackResponse(inputJson);
                }

            var envelopeJson = await response.Content.ReadAsStringAsync(cancellationToken);
            string rawContent;
            try
                {
                var envelope = JsonSerializer.Deserialize<OllamaChatResponse>(envelopeJson);
                rawContent = StripJsonFences(envelope?.Message?.Content ?? string.Empty);
                }
            catch (Exception ex)
                {
                _logger.LogWarning(ex, "No se pudo parsear el sobre de respuesta de Ollama para ClimateRisk.");
                return GetFallbackResponse(inputJson);
                }

            if (string.IsNullOrWhiteSpace(rawContent))
                return GetFallbackResponse(inputJson);

            return rawContent; // Devolvemos el JSON limpio para que la UI o el servicio lo parsee
            }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
            // Es un Timeout de HttpClient, NO una caída del servidor.
            // No marcamos IsModelReady = false para no apagar toda la IA.
            _logger.LogError(ex, "Timeout de Ollama en GetClimateRiskStrategyAsync tras esperar el tiempo configurado.");
            MarcarFalloDeGeneracion("Timeout", esFalloDeServidor: false);
            return GetFallbackResponse(inputJson);
            }
        catch (Exception ex)
            {
            _logger.LogError(ex, "Error inesperado en GetClimateRiskStrategyAsync");
            MarcarFalloDeGeneracion(ex.GetType().Name, esFalloDeServidor: true);
            return GetFallbackResponse(inputJson);
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

    // ==========================================
    // MÉTODO: Fallback para cuando la IA falla
    // ==========================================
    private static string GetFallbackResponse(string inputJson)
        {
        // Devolver un JSON válido pero genérico si la IA falla
        return JsonSerializer.Serialize(new
            {
            resumenImpacto = new
                {
                kilosPerdidosTotales = 0,
                porcentajePerdidaTotal = "N/D",
                tipoEventoMasFrecuente = "N/D",
                observaciones = "El servicio de IA no respondió o tardó demasiado. Se generó un plan genérico basado en datos disponibles."
                },
            planMitigacion = new[]
            {
            new { texto = "Revisar manualmente los eventos climáticos registrados y ajustar umbrales de detección.", categoria = "AjustePlaneacion", origen = "Checklist", impacto = "Asegurar precisión de datos", riesgo = "Bajo" },
            new { texto = "Considerar seguros agrícolas para cubrir pérdidas por eventos climáticos extremos.", categoria = "SeguroAgricola", origen = "Checklist", impacto = "Protección financiera", riesgo = "Costo de prima" },
            new { texto = "Mantener sistemas de riego antiheladas en alerta permanente durante invierno.", categoria = "Prevencion", origen = "Checklist", impacto = "Reducción de daño por heladas", riesgo = "Costo operativo" },
            new { texto = "Evaluar la diversificación de zonas de cultivo para mitigar riesgo regional.", categoria = "Diversificacion", origen = "Checklist", impacto = "Reducción de exposición", riesgo = "Inversión inicial" }
        }
            }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true });
        }

    // ==========================================
    // MÉTODO: Marcar fallo de generación
    // ==========================================
    private void MarcarFalloDeGeneracion(string motivo, bool esFalloDeServidor = true)
        {
        if (_connectionState.IsModelReady)
            _logger.LogWarning("Ollama respondió con error al generar ({Motivo}).", motivo);

        // Solo marcamos el modelo como no listo si es un fallo real del servidor (HTTP error, network down).
        // Un timeout o una respuesta vacía no significa que el modelo esté roto, solo que esa petición falló.
        if (esFalloDeServidor)
            _connectionState.IsModelReady = false;
        }

    // ==========================================
    // DTOs para la respuesta de IA de Clima
    // ==========================================
    public class ClimateAiResponse
        {
        public ResumenImpactoDto ResumenImpacto { get; set; } = new ResumenImpactoDto();
        public List<PlanMitigacionItemDto> PlanMitigacion { get; set; } = new List<PlanMitigacionItemDto>();
        }

    public class ResumenImpactoDto
        {
        public int KilosPerdidosTotales { get; set; }
        public decimal PorcentajePerdidaTotal { get; set; }
        public string TipoEventoMasFrecuente { get; set; } = string.Empty;
        public string Observaciones { get; set; } = string.Empty;
        }

    public class PlanMitigacionItemDto
        {
        public string Texto { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public string Origen { get; set; } = string.Empty;
        public string Impacto { get; set; } = string.Empty;
        public string Riesgo { get; set; } = string.Empty;
        }

    public class OllamaChatResponse
        {
        [JsonPropertyName("message")]
        public OllamaMessage? Message { get; set; }
        }

    public class OllamaMessage
        {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
        }
    }

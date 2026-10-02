namespace GuarinoApp.Infrastructure.Services;

using GuarinoApp.Application.Common;
using GuarinoApp.Application.Common.Dtos;
using GuarinoApp.Application.Common.Helpers;
using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.Contaduria.Dtos;
using GuarinoApp.Application.Modules.Proyeccion.Dtos;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class FallbackAIService : IAIService
    {
    private readonly OllamaAIService _ollamaService;
    private readonly ILogger<FallbackAIService> _logger;
    private readonly IPromptProvider _promptProvider;
    private readonly IDashboardDataService _dashboardService;
    private readonly ISkillRegistry _skillRegistry;
    private readonly IMcpDataService _mcpDataService;

    private const string FALLBACK_SUGGESTIONS_KEY = "FallbackSuggestions";
    private const string FALLBACK_ZAFRA_KEY = "FallbackZafra";

    public FallbackAIService(
        OllamaAIService ollamaService,
        ILogger<FallbackAIService> logger,
        IPromptProvider promptProvider,
        IDashboardDataService dashboardService,
        ISkillRegistry skillRegistry,
        IMcpDataService mcpDataService)
        {
        _ollamaService = ollamaService;
        _logger = logger;
        _promptProvider = promptProvider;
        _dashboardService = dashboardService;
        _skillRegistry = skillRegistry;
        _mcpDataService = mcpDataService;
        }

    public bool IsAvailable => _ollamaService.IsAvailable;

    public async Task<string> ChatAsync(string prompt, List<ChatMessage> history)
        {
        if (_ollamaService.IsAvailable)
            {
            try
                {
                var response = await _ollamaService.ChatAsync(prompt, history);
                if (!string.IsNullOrEmpty(response))
                    return response;
                }
            catch (Exception ex)
                {
                _logger.LogWarning(ex, "Ollama falló en ChatAsync, usando fallback.");
                }
            }

        return await BuildFallbackChatResponseAsync(prompt);
        }

    /// <summary>
    /// Respuesta analítica SIN IA: rutea la consulta por temas, usa primero los datos
    /// reales consultados (habilidades del sistema y fuentes externas en vivo) y arma
    /// hasta 5 puntos "Diagnóstico -> Acción -> Impacto" anclados en la telemetría del
    /// día. Nunca inventa cifras ni revela la tecnología interna: los ejes sin métrica
    /// se marcan con "Sin métrica interna" y aportan la práctica estándar de industria.
    /// </summary>
    private async Task<string> BuildFallbackChatResponseAsync(string userPrompt)
        {
        var fecha = DateTime.Now;
        var stockTotal = await _dashboardService.GetTotalStockAsync(fecha);
        var palletsSinCliente = await _dashboardService.GetPalletsWithoutClientAsync(fecha);
        var crecimiento = await _dashboardService.GetProjectedGrowthAsync(fecha);
        var stockPorVariedad = await _dashboardService.GetStockByVarietyAsync(fecha);
        var topVariedad = stockPorVariedad.OrderByDescending(kv => kv.Value).FirstOrDefault();
        var hayVariedades = stockPorVariedad is { Count: > 0 };

        var consulta = NormalizarConsulta(userPrompt);
        bool Pregunta(params string[] claves) => claves.Any(c => consulta.Contains(c, StringComparison.Ordinal));

        var sb = new StringBuilder();
        sb.AppendLine($"Lectura operativa al {fecha:dd/MM/yyyy} con los datos en vivo del sistema:");
        sb.AppendLine();

        // Datos reales del sistema: si la consulta corresponde a una habilidad
        // registrada, su resultado se muestra primero — son cifras verificables.
        try
            {
            var skillMatch = await _skillRegistry.MatchAsync(userPrompt ?? string.Empty);
            if (skillMatch is { Success: true })
                {
                sb.AppendLine("**Datos del sistema para esta consulta:**");
                sb.AppendLine(skillMatch.Text);
                sb.AppendLine();
                }
            else if (skillMatch != null)
                {
                sb.AppendLine($"**Consulta sin dato cargado:** {skillMatch.Text} El dato todavía no está registrado en el sistema.");
                sb.AppendLine();
                }
            }
        catch (Exception ex)
            {
            _logger.LogWarning(ex, "Fallback de chat: falló la consulta de datos del sistema; se continúa con telemetría.");
            }

        // Fuentes externas en vivo (orquestación determinista): van con su nombre de
        // origen y nunca pisan los datos internos de la planta.
        try
            {
            var externos = await _mcpDataService.GetContextAsync(userPrompt ?? string.Empty);
            if (externos.Count > 0)
                {
                sb.AppendLine("**Datos externos en vivo:**");
                foreach (var item in externos)
                    sb.AppendLine($"- [{item.Server}] {item.Content}");
                sb.AppendLine("Si contradicen los datos internos de la planta, priorizá los internos.");
                sb.AppendLine();
                }
            }
        catch (Exception ex)
            {
            _logger.LogWarning(ex, "Fallback de chat: fuentes externas no disponibles; se continúa con telemetría.");
            }

        // Puntos por tema: primero lo que el usuario preguntó, después el anclaje
        // con telemetría real hasta completar 3 puntos (máximo 5).
        var puntos = new List<string>();

        if (Pregunta("mercado", "export", "destino", "comercial", "precio", "cliente", "expansi", "vietnam", "india", "paises", "venta", "competencia"))
            puntos.Add("Sin métrica interna de precios por destino → explorar nuevos mercados de alto crecimiento (Asia: Vietnam e India) con las certificaciones al día → diversifica el ingreso y baja la exposición a pocos destinos.");

        if (Pregunta("calidad", "merma", "packout", "packing", "calibrac", "envas"))
            puntos.Add("Sin métrica interna de merma → fijar meta de packout y calendario de calibración óptica antes de la zafra → cada punto de merma evitado es ingreso exportable recuperado.");

        if (Pregunta("logistic", "flete", "contenedor", "puerto", "camion", "transporte", "flota", "zorra"))
            puntos.Add("Sin métrica interna de flete → relevar el costo por mercado y la ventana de contenedor para el pico → habilita decidir rutas y prioridades con tiempo.");

        if (Pregunta("clima", "helada", "lluvia", "riego", "agroclim", "viento", "calor"))
            puntos.Add("Sin métrica interna de clima en este panel → revisar el registro de eventos agroclimáticos y activar la prevención → anticipar el daño evita perder fruta ya formada.");

        if (Pregunta("caja", "capital", "finanz", "costo", "factura", "insumo", "credito", "flujo", "presupuesto"))
            puntos.Add("Sin métrica de caja en esta consulta → cruzar la proyección con Contaduría: capital necesario contra flujo de caja → dimensionar la brecha antes de comprometer insumos.");

        if (Pregunta("cosecha", "proceso", "cadena", "planta", "fruta", "zafra", "proyecc", "crecimiento", "capacidad", "planific"))
            puntos.Add($"Crecimiento proyectado de {crecimiento.ToString("0.0").Replace('.', ',')}% vs. período anterior → asegurar cajas, zunchos y turnos de empaque → evitar paradas de línea en el pico.");

        if (puntos.Count < 3 && palletsSinCliente > 0)
            puntos.Add($"Tenés {palletsSinCliente:N0} pallets sin cliente asignado → ofertarlos esta semana y reconciliar con los clientes → libera espacio de frío y convierte stock en caja.");

        if (puntos.Count < 3)
            puntos.Add($"Stock total de {stockTotal:N0} bins en planta → cruzar contra los compromisos de venta vigentes → detectar exceso antes que la merma y el costo de almacenaje.");

        if (puntos.Count < 3)
            puntos.Add($"Stock de {stockTotal:N0} bins con {crecimiento.ToString("0.0").Replace('.', ',')}% de crecimiento proyectado → mantener el monitoreo semanal de stock, pallets y empaque → anticipar cualquier desvío antes del pico.");

        if (puntos.Count < 4 && hayVariedades && stockTotal > 0 && topVariedad.Value > stockTotal * 0.4m)
            puntos.Add($"La variedad {topVariedad.Key} concentra {topVariedad.Value:N0} de {stockTotal:N0} bins → abrir conversaciones comerciales para ese volumen → reduce la exposición al precio de una sola variedad.");

        foreach (var punto in puntos.Take(5))
            sb.AppendLine($"- {punto}");

        return sb.ToString().TrimEnd();
        }

    /// <summary>
    /// Minúsculas y sin acentos para rutar la consulta por temas
    /// ("expansión" y "expansion" llegan iguales a los detectores).
    /// </summary>
    private static string NormalizarConsulta(string? texto)
        {
        if (string.IsNullOrWhiteSpace(texto))
            return string.Empty;
        var descompuesto = texto.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var limpio = new StringBuilder(descompuesto.Length);
        foreach (var c in descompuesto)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                limpio.Append(c);
        return limpio.ToString();
        }

    public async Task<List<BusinessSuggestionDto>> GetBusinessSuggestionsAsync(
        BusinessSuggestionRequest request,
        List<ChatMessage>? history = null)
        {
        if (_ollamaService.IsAvailable)
            {
            try
                {
                var aiSuggestions = await _ollamaService.GetBusinessSuggestionsAsync(request, history);
                if (aiSuggestions != null && aiSuggestions.Any())
                    return aiSuggestions;
                }
            catch (Exception ex)
                {
                _logger.LogWarning(ex, "Ollama falló en GetBusinessSuggestionsAsync, usando fallback.");
                }
            }

        _logger.LogInformation("Sugerencias: Ollama no aportó resultados; usando fallback de reglas.");
        return await BuildFallbackSuggestionsAsync(request);
        }

    private async Task<List<BusinessSuggestionDto>> BuildFallbackSuggestionsAsync(BusinessSuggestionRequest request)
        {
        var template = await _promptProvider.GetPromptAsync(FALLBACK_SUGGESTIONS_KEY);

        if (!string.IsNullOrWhiteSpace(template))
            {
            // Plantilla reservada para uso futuro; hoy el fallback usa reglas declarativas abajo.
            }

        var result = new List<BusinessSuggestionDto>();

        if (request.PalletsWithoutClient > 0)
            {
            result.Add(new BusinessSuggestionDto
                {
                Text = $"Gestionar {request.PalletsWithoutClient} pallets sin cliente asignado. Priorizar ofertas comerciales para liberar espacio.",
                Color = "#FF9800",
                Icon = "warning"
                });
            }

        if (request.GrowthPercent > 3m)
            {
            result.Add(new BusinessSuggestionDto
                {
                Text = $"Crecimiento proyectado del {request.GrowthPercent}%. Verificar stock de insumos (cajas, zunchos) para evitar paradas.",
                Color = "#4CAF50",
                Icon = "trending_up"
                });
            }

        if (request.StockByVariety != null && request.StockByVariety.Any())
            {
            var topVar = request.StockByVariety.OrderByDescending(kv => kv.Value).FirstOrDefault();
            if (topVar.Value > request.TotalStock * 0.4m)
                {
                result.Add(new BusinessSuggestionDto
                    {
                    Text = $"Alta concentración en {topVar.Key} ({topVar.Value} bins). Evaluar diversificación de mercado para mitigar riesgo de precio.",
                    Color = "#2196F3",
                    Icon = "pie_chart"
                    });
                }
            }

        if (result.Count == 0)
            {
            result.Add(new BusinessSuggestionDto
                {
                Text = "Escenario estable. Monitorear indicadores clave de rendimiento (KPIs) semanalmente.",
                Color = "#009688",
                Icon = "check_circle"
                });
            }

        // El panel promete "sugerencias" (plural): aunque las reglas activen
        // pocos puntos, se completa hasta 3 con alternativas derivadas de datos.
        var extras = new List<BusinessSuggestionDto>
            {
            new()
                {
                Text = $"Stock actual en planta: {request.TotalStock:N0} bins. Verificar compromisos de compra vigentes contra el plan.",
                Color = "#1976D2",
                Icon = "inventory"
                },
            new()
                {
                Text = "Monitorear clima y riesgo agrícola antes de comprometer volúmenes de compra.",
                Color = "#009688",
                Icon = "wb_sunny"
                },
            new()
                {
                Text = "Revisar semanalmente indicadores de empaque, logística y liquidaciones con la última data disponible.",
                Color = "#9C27B0",
                Icon = "assessment"
                }
            };

        foreach (var extra in extras)
            {
            if (result.Count >= 3)
                break;
            result.Add(extra);
            }

        return result;
        }

    public async Task<ZafraStrategyResultDto> GetZafraInteligentStrategyAsync(
        ZafraDto projectedZafra,
        decimal capacidadMaxHistorica,
        int stockActualBins,
        List<BusinessSuggestionDto>? history = null,
        FinancialDataDto? datosFinancieros = null)
        {
        if (_ollamaService.IsAvailable)
            {
            try
                {
                var aiResult = await _ollamaService.GetZafraInteligentStrategyAsync(projectedZafra, capacidadMaxHistorica, stockActualBins, history, datosFinancieros);
                if (aiResult != null && (aiResult.AccionesEstrategicas.Any() || aiResult.KPIs != null))
                    return aiResult;
                }
            catch (Exception ex)
                {
                _logger.LogWarning(ex, "Ollama falló en GetZafraInteligentStrategyAsync, usando fallback.");
                }
            }

        return BuildFallbackZafraStrategy(projectedZafra, capacidadMaxHistorica, stockActualBins, datosFinancieros);
        }

    public async Task<ModuloInsightDto> GetAnalisisDeModuloAsync(string clavePrompt, string contextoJson)
        {
        if (_ollamaService.IsAvailable)
            {
            try
                {
                var aiResult = await _ollamaService.GetAnalisisDeModuloAsync(clavePrompt, contextoJson);
                if (aiResult is { EsIA: true })
                    return aiResult;
                }
            catch (Exception ex)
                {
                _logger.LogWarning(ex, "Ollama falló en GetAnalisisDeModuloAsync ({Clave}); la página conserva su análisis local.", clavePrompt);
                }
            }

        // Sin IA: vacío con EsIA=false. El análisis local determinista lo construye la
        // propia página con los mismos datos que muestra en pantalla (no se inventa texto).
        return new ModuloInsightDto();
        }

    /// <summary>
    /// Fallback sin IA. Los KPIs financieros, la comparativa de benchmark, los
    /// escenarios, el plan de acciones (PlanAccionZafraBuilder) y el resumen
    /// ejecutivo salen de las mismas fuentes que usa la página — de modo que lo
    /// visible sea idéntico con o sin motor de IA. Los ejes sin fuente de datos
    /// conectada se agregan como recordatorios operativos estándar de industria
    /// (Origen="Checklist"), nunca como cifras inventadas.
    /// </summary>
    private static ZafraStrategyResultDto BuildFallbackZafraStrategy(
        ZafraDto projectedZafra,
        decimal capacidadMaxHistorica,
        int stockActualBins,
        FinancialDataDto? datosFinancieros)
        {
        var volumenProyectado = projectedZafra.TotalKgs;

        var kpis = FinancialKpiCalculator.Calcular(volumenProyectado, capacidadMaxHistorica, datosFinancieros);
        kpis.ComparativaBenchmark = FinancialKpiCalculator.ConstruirComparativaBenchmark(
            projectedZafra, capacidadMaxHistorica, datosFinancieros);

        var escenarios = FinancialKpiCalculator.ConstruirEscenarios(volumenProyectado, datosFinancieros);

        // Plan de acciones y resumen: misma construcción determinista que usa la página,
        // de modo que lo visible sea idéntico venga la IA o no.
        var acciones = PlanAccionZafraBuilder.Construir(projectedZafra, capacidadMaxHistorica, stockActualBins, datosFinancieros, kpis);
        var resumen = FinancialKpiCalculator.ConstruirResumenEjecutivo(projectedZafra, kpis, datosFinancieros);

        return new ZafraStrategyResultDto
            {
            KPIs = kpis,
            AccionesEstrategicas = acciones,
            Escenarios = escenarios,
            ResumenEjecutivo = resumen,
            EsIA = false
            };
        }
    }

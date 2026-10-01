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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class FallbackAIService : IAIService
    {
    private readonly OllamaAIService _ollamaService;
    private readonly ILogger<FallbackAIService> _logger;
    private readonly IPromptProvider _promptProvider;
    private readonly IDashboardDataService _dashboardService;

    private const string FALLBACK_CHAT_KEY = "FallbackChat";
    private const string FALLBACK_SUGGESTIONS_KEY = "FallbackSuggestions";
    private const string FALLBACK_ZAFRA_KEY = "FallbackZafra";

    public FallbackAIService(
        OllamaAIService ollamaService,
        ILogger<FallbackAIService> logger,
        IPromptProvider promptProvider,
        IDashboardDataService dashboardService)
        {
        _ollamaService = ollamaService;
        _logger = logger;
        _promptProvider = promptProvider;
        _dashboardService = dashboardService;
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

    private async Task<string> BuildFallbackChatResponseAsync(string userPrompt)
        {
        var fecha = DateTime.Now;
        var stockTotal = await _dashboardService.GetTotalStockAsync(fecha);
        var palletsSinCliente = await _dashboardService.GetPalletsWithoutClientAsync(fecha);
        var crecimiento = await _dashboardService.GetProjectedGrowthAsync(fecha);
        var stockPorVariedad = await _dashboardService.GetStockByVarietyAsync(fecha);
        var topVariedad = stockPorVariedad.OrderByDescending(kv => kv.Value).FirstOrDefault();

        var template = await _promptProvider.GetPromptAsync(FALLBACK_CHAT_KEY);

        if (string.IsNullOrWhiteSpace(template))
            {
            var sb = new StringBuilder();
            sb.AppendLine($"No se pudo conectar con el motor de IA. Aquí tienes un resumen operativo actualizado ({fecha:dd/MM/yyyy}):");
            sb.AppendLine();
            sb.AppendLine($"- **Stock Total:** {stockTotal:N0} bins");
            sb.AppendLine($"- **Variedad Principal:** {topVariedad.Key} ({topVariedad.Value:N0} bins)");
            sb.AppendLine($"- **Pallets sin Cliente:** {palletsSinCliente:N0}");
            sb.AppendLine($"- **Crecimiento Proyectado:** {crecimiento:0.0}%");
            sb.AppendLine();
            sb.AppendLine("Para respuestas estratégicas detalladas, por favor active Ollama.");
            return sb.ToString();
            }

        return template
            .Replace("{Date}", fecha.ToString("yyyy-MM-dd"))
            .Replace("{TotalStock}", stockTotal.ToString("N0"))
            .Replace("{PalletsWithoutClient}", palletsSinCliente.ToString("N0"))
            .Replace("{GrowthPercent}", crecimiento.ToString("0.0"))
            .Replace("{TopVariety}", string.IsNullOrEmpty(topVariedad.Key) ? "N/A" : topVariedad.Key)
            .Replace("{TopVarietyValue}", topVariedad.Value.ToString("N0"))
            .Replace("{UserPrompt}", userPrompt);
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

    /// <summary>
    /// Fallback sin IA. Los KPIs financieros, la comparativa de benchmark y los
    /// escenarios salen de FinancialKpiCalculator —la misma fuente que usa la
    /// página— de modo que los números sean idénticos con o sin motor de IA.
    /// Los ejes sin fuente de datos conectada (calidad, logística, mano de obra,
    /// fitosanitario, costo comparado) se agregan como recordatorios operativos
    /// estándar de industria (Origen="Checklist"), nunca como cifras inventadas
    /// -- mismos ejes que pide ZafraStrategy.txt, para que el plan se vea igual
    /// con o sin IA disponible.
    /// </summary>
    private static ZafraStrategyResultDto BuildFallbackZafraStrategy(
        ZafraDto projectedZafra,
        decimal capacidadMaxHistorica,
        int stockActualBins,
        FinancialDataDto? datosFinancieros)
        {
        var esSimulado = projectedZafra.EsDummy;
        var variacion = projectedZafra.VariacionPorcentual;
        var volumenProyectado = projectedZafra.TotalKgs;

        var kpis = FinancialKpiCalculator.Calcular(volumenProyectado, capacidadMaxHistorica, datosFinancieros);
        kpis.ComparativaBenchmark = FinancialKpiCalculator.ConstruirComparativaBenchmark(
            projectedZafra, capacidadMaxHistorica, datosFinancieros);

        var escenarios = FinancialKpiCalculator.ConstruirEscenarios(volumenProyectado, datosFinancieros);

        var acciones = new List<BusinessSuggestionDto>();

        // --- Financiero / Logística de capacidad (Calculado: datos reales disponibles acá) ---
        if (esSimulado)
            {
            acciones.Add(new BusinessSuggestionDto
                {
                Text = "⚠️ Datos de proyección son simulados. Validar con datos reales de campo antes de comprometer recursos financieros mayores.",
                Color = "#FF9800",
                Icon = "warning",
                Categoria = "Financiero",
                Origen = "Checklist",
                Riesgo = "Decisiones tomadas sobre datos simulados pueden no reflejar la realidad de campo."
                });
            }

        if (kpis.BrechaLiquidez > 0 && datosFinancieros != null)
            {
            acciones.Add(new BusinessSuggestionDto
                {
                Text = $"La caja disponible no cubre el capital requerido para la zafra: faltan ${kpis.BrechaLiquidez:N0} " +
                       $"(capital ${kpis.CapitalNecesario:N0} vs. caja ${datosFinancieros.FlujoCaja:N0} — riesgo {kpis.RiesgoLiquidez}). " +
                       "Gestionar crédito de temporada, adelanto de clientes o reprogramación de pagos ANTES del pico.",
                Color = "#F44336",
                Icon = "account_balance_wallet",
                Categoria = "Financiero",
                Origen = "Calculado",
                Impacto = $"Brecha de ${kpis.BrechaLiquidez:N0} a cubrir antes del inicio de zafra.",
                Riesgo = "Sin financiamiento oportuno, la zafra se frena en el pico cuando el volumen ya está comprometido con clientes."
                });
            }

        if (capacidadMaxHistorica > 0 && volumenProyectado > capacidadMaxHistorica * 1.1m)
            {
            var exceso = volumenProyectado - capacidadMaxHistorica;
            acciones.Add(new BusinessSuggestionDto
                {
                Text = $"La proyección supera la capacidad histórica en {exceso:N0} kg. Evaluar contratación de turno extra o tercerización de empaque.",
                Color = "#F44336",
                Icon = "speed",
                Categoria = "ManoDeObra",
                Origen = "Calculado",
                Impacto = $"Exceso de {exceso:N0} kg sobre capacidad histórica.",
                Riesgo = "Sin turno extra, riesgo de cuello de botella en el pico de zafra."
                });
            }
        else if (stockActualBins > 0 && volumenProyectado < capacidadMaxHistorica * 0.8m)
            {
            acciones.Add(new BusinessSuggestionDto
                {
                Text = "Volumen proyectado inferior al histórico. Optimizar turnos de empaque para reducir costos fijos y liberar capacidad de frío.",
                Color = "#2196F3",
                Icon = "trending_down",
                Categoria = "ManoDeObra",
                Origen = "Calculado"
                });
            }

        if (variacion > 10)
            {
            var incremento = (int)(stockActualBins * 0.15);
            acciones.Add(new BusinessSuggestionDto
                {
                Text = $"Crecimiento proyectado del {variacion:F1}%. Anticipar compra de insumos para {incremento} bins adicionales y asegurar precios.",
                Color = "#4CAF50",
                Icon = "shopping_cart",
                Categoria = "Financiero",
                Origen = "Calculado",
                Impacto = $"{incremento} bins adicionales de insumos estimados."
                });
            }
        else if (variacion < -5)
            {
            acciones.Add(new BusinessSuggestionDto
                {
                Text = $"Descenso proyectado del {Math.Abs(variacion):F1}%. Revisar contratos de insumos para reducir volúmenes y liberar flujo de caja.",
                Color = "#9C27B0",
                Icon = "account_balance",
                Categoria = "Financiero",
                Origen = "Calculado"
                });
            }

        // --- Ejes sin fuente de datos real conectada todavía: recordatorio operativo estándar ---
        acciones.Add(new BusinessSuggestionDto
            {
            Text = "Definir meta de packout rate para la próxima zafra y plan de calibración óptica antes del pico.",
            Color = "#795548",
            Icon = "grading",
            Categoria = "Calidad",
            Origen = "Checklist",
            Riesgo = "Sin esta revisión, la merma real puede superar lo presupuestado sin que se detecte a tiempo."
            });

        acciones.Add(new BusinessSuggestionDto
            {
            Text = "Confirmar disponibilidad de contenedores refrigerados y turnos de puerto para el volumen proyectado del pico.",
            Color = "#3F51B5",
            Icon = "local_shipping",
            Categoria = "Logistica",
            Origen = "Checklist"
            });

        acciones.Add(new BusinessSuggestionDto
            {
            Text = "Relevar disponibilidad de personal temporal en la zona antes del inicio del pico de zafra.",
            Color = "#009688",
            Icon = "groups",
            Categoria = "ManoDeObra",
            Origen = "Checklist"
            });

        acciones.Add(new BusinessSuggestionDto
            {
            Text = "Verificar vigencia de certificaciones (GlobalGAP, PPECB u otras según mercado destino) antes del inicio de zafra.",
            Color = "#8BC34A",
            Icon = "verified",
            Categoria = "Fitosanitario",
            Origen = "Checklist",
            Riesgo = "Un rechazo fitosanitario en destino invalida la proyección financiera de ese lote."
            });

        acciones.Add(new BusinessSuggestionDto
            {
            Text = "Relevar costo de flete actual por mercado de destino para poder comparar contra benchmark internacional en la próxima ronda.",
            Color = "#607D8B",
            Icon = "compare_arrows",
            Categoria = "CostoComparado",
            Origen = "Checklist"
            });

        return new ZafraStrategyResultDto
            {
            KPIs = kpis,
            AccionesEstrategicas = acciones,
            Escenarios = escenarios
            };
        }
    }

using System.Text.Json;

namespace GuarinoApp.Application.Modules.ClimateRisk.Dtos;

public class ClimateAiResponse
    {
    // Se recibe en crudo: el plan de mitigación usa solo PlanMitigacion, y el prompt
    // pide que porcentajePerdidaTotal venga como número o "N/D" (a veces como texto).
    // Parsearlo estricto como decimal hacía fallar la deserialización COMPLETA de la
    // respuesta —incluso la del propio planMitigacion— y todo el plan caía al
    // respaldo "Sistema (sin IA)" aunque la IA hubiera respondido bien.
    public JsonElement ResumenImpacto { get; set; }

    public List<PlanMitigacionItemDto> PlanMitigacion { get; set; } = [];
    }

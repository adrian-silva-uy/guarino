namespace GuarinoApp.Application.Modules.ClimateRisk.Dtos;

public class ClimateAiResponse
    {
    public ResumenImpactoDto ResumenImpacto { get; set; } = new();
    public List<PlanMitigacionItemDto> PlanMitigacion { get; set; } = [];
    }

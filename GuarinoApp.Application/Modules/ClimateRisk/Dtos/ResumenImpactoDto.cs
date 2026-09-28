namespace GuarinoApp.Application.Modules.ClimateRisk.Dtos;

public class ResumenImpactoDto
    {
    public int KilosPerdidosTotales { get; set; }
    public decimal PorcentajePerdidaTotal { get; set; }
    public string TipoEventoMasFrecuente { get; set; } = "N/D";
    public string Observaciones { get; set; } = string.Empty;
    }

namespace GuarinoApp.Infrastructure.Services;

public class ResumenImpactoDto
    {
    public int KilosPerdidosTotales { get; set; }
    public decimal PorcentajePerdidaTotal { get; set; }
    public string TipoEventoMasFrecuente { get; set; } = string.Empty;
    public string Observaciones { get; set; } = string.Empty;
    }

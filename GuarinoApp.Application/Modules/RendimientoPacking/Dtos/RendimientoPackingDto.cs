namespace GuarinoApp.Application.Modules.RendimientoPacking.Dtos;

public class RendimientoPackingDto
{
    public int Zafra { get; set; }
    public string? Variedad { get; set; }
    public string? Productor { get; set; }
    public decimal KgVolcados { get; set; }
    public decimal KgExportable { get; set; }
    public decimal KgMercadoInterno { get; set; }
    public decimal KgDescarte { get; set; }

    public decimal PctExportable => KgVolcados > 0 ? KgExportable / KgVolcados * 100 : 0;
    public decimal PctMercadoInterno => KgVolcados > 0 ? KgMercadoInterno / KgVolcados * 100 : 0;
    public decimal PctDescarte => KgVolcados > 0 ? KgDescarte / KgVolcados * 100 : 0;
}

public class RendimientoPackingResultDto
{
    public List<RendimientoPackingDto> Detalle { get; set; } = new();
    public decimal TotalKgVolcados => Detalle.Sum(d => d.KgVolcados);
    public decimal TotalKgExportable => Detalle.Sum(d => d.KgExportable);
    public decimal TotalKgMercadoInterno => Detalle.Sum(d => d.KgMercadoInterno);
    public decimal TotalKgDescarte => Detalle.Sum(d => d.KgDescarte);
    public decimal PctTotalExportable => TotalKgVolcados > 0 ? TotalKgExportable / TotalKgVolcados * 100 : 0;
    public decimal PctTotalMercadoInterno => TotalKgVolcados > 0 ? TotalKgMercadoInterno / TotalKgVolcados * 100 : 0;
    public decimal PctTotalDescarte => TotalKgVolcados > 0 ? TotalKgDescarte / TotalKgVolcados * 100 : 0;
}

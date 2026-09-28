namespace GuarinoApp.Application.Modules.ResumenLiquidaciones.Dtos;
public class ResumenLiquidacionDto
    {
    public string? Productor { get; set; }
    public string? Variedad { get; set; }
    public int Año { get; set; }
    public decimal TotalKgs { get; set; }
    public int TotalBins { get; set; }
    public decimal TotalPagar { get; set; }
    }

namespace GuarinoApp.Application.Modules.ResumenLiquidaciones.Dtos;
public class ResumenLiquidacionesDto
    {
    public int IdLiquidacion { get; set; }
    public string? Productor { get; set; }
    public string? Zafra { get; set; }
    public DateTime? FechaLiquidacion { get; set; }
    public decimal TotalKgs { get; set; }
    public decimal TotalImporte { get; set; }
    }

namespace GuarinoApp.Application.Modules.Contaduria.Dtos;

public class FinancialDataDto
{
    public int Id { get; set; }
    public decimal CostoPorKg { get; set; }
    public decimal FlujoCaja { get; set; }
    public decimal MargenBruto { get; set; }
    public decimal? CostoFlete { get; set; }
    public decimal CapacidadInstalada { get; set; }
    public DateTime FechaActualizacion { get; set; }
    public string? ActualizadoPor { get; set; }
    public string? Observaciones { get; set; }
}

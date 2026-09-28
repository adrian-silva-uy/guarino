namespace GuarinoApp.Application.Modules.Empaque.Dtos;
public class EmpaqueDto
    {
    public string? TipoEmpaque { get; set; }
    public int TotalPallets { get; set; }
    public int TotalEnvases { get; set; }
    public decimal PesoTotal { get; set; }
    public int? CapacidadMaxima { get; set; }
    public int? Ocupacion { get; set; }
    }

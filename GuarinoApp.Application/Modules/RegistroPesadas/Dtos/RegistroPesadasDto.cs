namespace GuarinoApp.Application.Modules.RegistroPesadas.Dtos;
public class RegistroPesadasDto
    {
    public int IdPesada { get; set; }
    public string? LoteCodigo { get; set; }
    public string? Bascula { get; set; }
    public DateTime? FechaPesada { get; set; }
    public decimal Peso { get; set; }
    public string? Responsable { get; set; }
    }

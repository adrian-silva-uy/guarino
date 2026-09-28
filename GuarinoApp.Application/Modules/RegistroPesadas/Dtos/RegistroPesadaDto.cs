namespace GuarinoApp.Application.Modules.RegistroPesadas.Dtos;
public class RegistroPesadaDto
    {
    public int Id { get; set; }
    public DateTime? Fecha { get; set; }
    public string? Lote { get; set; }
    public string? Productor { get; set; }
    public string? Bascula { get; set; }
    public decimal Peso { get; set; }
    public int Bins { get; set; }
    }

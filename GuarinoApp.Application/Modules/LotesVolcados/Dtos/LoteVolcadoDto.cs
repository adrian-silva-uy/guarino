namespace GuarinoApp.Application.Modules.LotesVolcados.Dtos;
public class LoteVolcadoDto
    {
    public int IdLote { get; set; }
    public string? Codigo { get; set; }
    public string? Variedad { get; set; }
    public string? Productor { get; set; }
    public DateTime? FechaVolcado { get; set; }
    public decimal KilosVolcados { get; set; }
    public int BinsVolcados { get; set; }
    }

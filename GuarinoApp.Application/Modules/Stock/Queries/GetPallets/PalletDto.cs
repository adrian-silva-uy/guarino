namespace GuarinoApp.Application.Modules.Stock.Queries.GetPallets;
public class PalletDto
    {
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string LoteCodigo { get; set; } = string.Empty;
    public string Variedad { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public string Cliente { get; set; } = string.Empty;
    public string? Zafra { get; set; }
    }

namespace GuarinoApp.Application.Modules.StockPlaya.Dtos;
public class StockPlayaDto
    {
    public string? Empaque { get; set; }
    public string? Especie { get; set; }
    public string? Variedad { get; set; }
    public int? Lote { get; set; }
    public string? Balanza { get; set; }
    public string? Destino { get; set; }
    public string? Feccha { get; set; }
    public string? Chacra { get; set; }
    public string? Productor { get; set; }
    public string? TipoLote { get; set; }
    public int Ingresados { get; set; }
    public int Volcados { get; set; }
    public int Saldo { get; set; }
    public decimal KgIngreso { get; set; }
    public decimal KgVolcados { get; set; }
    public decimal KgStock { get; set; }
    }

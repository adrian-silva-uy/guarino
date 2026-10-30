namespace GuarinoApp.Application.Modules.CostoDesglose.Dtos;

public class CostoDesgloseKgDto
{
    public int Id { get; set; }
    public int Zafra { get; set; }
    public decimal CostoFrutaPlanta { get; set; }
    public decimal CostoPackingManoObra { get; set; }
    public decimal CostoCartonEnvases { get; set; }
    public decimal CostoEnergiaFrio { get; set; }
    public decimal CostoFletePuerto { get; set; }
    public decimal CostoOtros { get; set; }
    public decimal KgExportablesProyectados { get; set; }
    public DateTime FechaCarga { get; set; }
    public string? CargadoPor { get; set; }
    public string? Observaciones { get; set; }

    public decimal CostoTotal =>
        CostoFrutaPlanta + CostoPackingManoObra + CostoCartonEnvases +
        CostoEnergiaFrio + CostoFletePuerto + CostoOtros;

    public decimal CostoPorKgExportable =>
        KgExportablesProyectados > 0 ? CostoTotal / KgExportablesProyectados : 0m;
}

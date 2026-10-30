using GuarinoApp.Domain.Shared;

namespace GuarinoApp.Domain.Entities;

/// <summary>
/// Desglose del costo real por kg exportable, cargado por Contaduría o Gerencia.
/// Permite saber cuánto cuesta dejar un kg listo para exportar, desglosado por
/// rubro: fruta en planta, packing, cartón/envases, energía y frío, flete y
/// puerto, y otros. Se usa para calcular el costo por kg exportable que alimenta
/// el capital de trabajo y la brecha de caja de Proyección de Zafra.
/// </summary>
public class CostoDesgloseKg : BaseEntity
{
    public int Zafra { get; set; }

    /// <summary>Precio pagado al productor o costo propio de quinta (fruta puesta en planta).</summary>
    public decimal CostoFrutaPlanta { get; set; }

    /// <summary>Mano de obra de packing.</summary>
    public decimal CostoPackingManoObra { get; set; }

    /// <summary>Cartón y envases.</summary>
    public decimal CostoCartonEnvases { get; set; }

    /// <summary>Energía y frío.</summary>
    public decimal CostoEnergiaFrio { get; set; }

    /// <summary>Flete a Montevideo y gastos de puerto.</summary>
    public decimal CostoFletePuerto { get; set; }

    /// <summary>Otros costos operativos.</summary>
    public decimal CostoOtros { get; set; }

    /// <summary>Kg exportables proyectados para la zafra (base del cálculo unitario).</summary>
    public decimal KgExportablesProyectados { get; set; }

    public DateTime FechaCarga { get; set; } = DateTime.Now;

    public string? CargadoPor { get; set; }

    public string? Observaciones { get; set; }

    /// <summary>Suma de todos los rubros de costo.</summary>
    public decimal CostoTotal =>
        CostoFrutaPlanta + CostoPackingManoObra + CostoCartonEnvases +
        CostoEnergiaFrio + CostoFletePuerto + CostoOtros;

    /// <summary>Costo total dividido entre kg exportables proyectados.</summary>
    public decimal CostoPorKgExportable =>
        KgExportablesProyectados > 0 ? CostoTotal / KgExportablesProyectados : 0m;
}

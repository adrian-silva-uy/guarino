using GuarinoApp.Domain.Shared;

namespace GuarinoApp.Domain.Entities;

/// <summary>
/// Datos financieros ingresados por Contaduría para cálculos de KPIs.
/// </summary>
public class FinancialData : BaseEntity
{
    /// <summary>
    /// Costo operativo promedio por kg (últimos 3 años)
    /// </summary>
    public decimal CostoPorKg { get; set; }

    /// <summary>
    /// Flujo de caja actual disponible
    /// </summary>
    public decimal FlujoCaja { get; set; }

    /// <summary>
    /// Margen bruto histórico promedio (%)
    /// </summary>
    public decimal MargenBruto { get; set; }

    /// <summary>
    /// Costo de flete promedio por contenedor
    /// </summary>
    public decimal? CostoFlete { get; set; }

    /// <summary>
    /// Capacidad instalada máxima (kg por zafra)
    /// </summary>
    public decimal CapacidadInstalada { get; set; }

    /// <summary>
    /// Fecha de actualización de los datos
    /// </summary>
    public DateTime FechaActualizacion { get; set; } = DateTime.Now;

    /// <summary>
    /// Usuario que actualizó los datos
    /// </summary>
    public string? ActualizadoPor { get; set; }

    /// <summary>
    /// Observaciones o notas adicionales
    /// </summary>
    public string? Observaciones { get; set; }
}

namespace GuarinoApp.Application.Modules.Contaduria.Dtos;

/// <summary>
/// Datos operativos que el módulo de Contaduría NO carga a mano: se calculan
/// automáticamente desde otras tablas (liquidaciones, pesadas/emboquille y
/// pallets) para enriquecer la tarjeta de análisis y el INPUT de la IA.
/// Cada campo es null cuando NO hay datos reales (nunca se rellena con el
/// fallback de ejemplo de las queries: la IA no puede recibir cifras inventadas).
/// </summary>
public sealed class ContextoOperativoContaduriaDto
{
    /// <summary>Año de referencia usado para los totales (año de la zafra proyectada o el actual).</summary>
    public int Anio { get; set; }

    // --- Liquidaciones a productores (tabla Lotes) ---
    /// <summary>Suma a pagar a productores del año (mismo cálculo que Resumen de Liquidaciones).</summary>
    public decimal? PagarAProductores { get; set; }
    public decimal? LiquidacionesKgs { get; set; }
    public int? LiquidacionesBins { get; set; }

    // --- Ejecutado de la zafra (LotesEmboquille, igual que Resumen Mensual) ---
    /// <summary>Kilos volcados acumulados en el año de la zafra.</summary>
    public decimal? KgsVolcadosAnio { get; set; }

    // --- Stock en patio (LotesEmboquille con Estado "E", igual que Stock en Playa) ---
    public decimal? StockPlayaKg { get; set; }

    // --- Pallets sin cliente (tabla Pallets, igual que el módulo Stock de Pallets Sin Cliente) ---
    public int? PalletsSinCliente { get; set; }
    public decimal? PalletsSinClienteKg { get; set; }

    /// <summary>True si al menos un campo trae datos reales (para mostrar la tarjeta).</summary>
    public bool HayDatos =>
        PagarAProductores.HasValue || KgsVolcadosAnio.HasValue ||
        StockPlayaKg.HasValue || PalletsSinCliente.HasValue;
}

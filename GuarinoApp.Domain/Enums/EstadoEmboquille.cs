namespace GuarinoApp.Domain.Enums;

/// <summary>
/// Estados del registro de emboquille (columna LotesEmboquille.Estado).
/// Son los valores textuales heredados del sistema DTrace y se conservan
/// EXACTAMENTE así en la base de datos (no se cambian los datos); en el
/// código se usan estas constantes con nombre para que cada filtro diga
/// qué representa en vez de una letra suelta.
///
/// Significado real:
/// - EnPlaya ("E"): fruta que quedó en la playa (patio) a la espera de
///   despacho o volcado — es lo que muestra la página Stock en Playa.
/// - Despachado ("D"): el registro se cerró porque sus bins salieron despachados.
/// - Volcado ("V"): el registro se cerró porque sus bins fueron volcados al empaque.
/// - Finalizado: valor legado que escribe la siembra antigua (SeedProyeccionZafra).
/// </summary>
public static class EstadoEmboquille
{
    /// <summary>En playa: fruta en patio esperando despacho o volcado.</summary>
    public const string EnPlaya = "E";

    /// <summary>Cerrado como despachado.</summary>
    public const string Despachado = "D";

    /// <summary>Cerrado como volcado al empaque.</summary>
    public const string Volcado = "V";

    /// <summary>Valor legado de la siembra antigua (SeedProyeccionZafra).</summary>
    public const string Finalizado = "Finalizado";
}

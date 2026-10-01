namespace GuarinoApp.Application.Common.Dtos;

/// <summary>
/// Análisis de un módulo funcional (Contaduría, Gerencia, …). La página construye
/// siempre una versión local determinista con los datos visibles; si el motor de IA
/// responde y el parseo es correcto, se reemplaza el texto y se enciende <c>EsIA</c>.
/// </summary>
public class ModuloInsightDto
{
    /// <summary>Lectura ejecutiva en 3–4 oraciones con cifras concretas.</summary>
    public string Lectura { get; set; } = string.Empty;

    /// <summary>Hasta 4 hallazgos con severidad (alta/media/baja).</summary>
    public List<InsightHallazgoDto> Hallazgos { get; set; } = new();

    /// <summary>Hasta 4 acciones concretas con plazo e impacto.</summary>
    public List<InsightAccionDto> Acciones { get; set; } = new();

    /// <summary>true solo cuando el contenido proviene de la IA parseado sin errores;
    /// el gate de las páginas evita pintar basura si el parseo falló.</summary>
    public bool EsIA { get; set; }
}

public class InsightHallazgoDto
{
    public string Titulo { get; set; } = string.Empty;

    public string Detalle { get; set; } = string.Empty;

    /// <summary>alta | media | baja</summary>
    public string Severidad { get; set; } = "media";
}

public class InsightAccionDto
{
    public string Texto { get; set; } = string.Empty;

    /// <summary>Ej.: "inmediato", "esta semana", "antes de la zafra".</summary>
    public string Plazo { get; set; } = string.Empty;

    /// <summary>Ej.: "libera $12.000 de caja", "reduce riesgo de quiebre de stock".</summary>
    public string Impacto { get; set; } = string.Empty;
}

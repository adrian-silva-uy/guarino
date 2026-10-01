namespace GuarinoApp.Domain.Enums;

/// <summary>
/// Única fuente de verdad para traducir entre el enum EstadoPallet, la etiqueta
/// legible que muestra la UI y los códigos legados del SQL histórico
/// ('D'/'F'/'P'/'R'/'B'). Centralizar acá estas 5 equivalencias evita que cada
/// handler re-implemente su propio mapeo (había dos copias: una aquí y otra
/// invertida en GetPalletsQueryHandler).
/// </summary>
public static class EstadoPalletExtensions
    {
    private static readonly Dictionary<string, EstadoPallet> PorEtiquetaOCodigo =
        new(StringComparer.OrdinalIgnoreCase)
        {
        // Etiquetas legibles (las que ve el usuario)
        ["Despachado"] = EstadoPallet.Despachado,
        ["En Frío"] = EstadoPallet.EnFrio,
        ["Pre Frío"] = EstadoPallet.PreFrio,
        ["Rearmado"] = EstadoPallet.Rearmado,
        ["Bajado"] = EstadoPallet.Bajado,
        // Códigos legados que traían los datos históricos
        ["D"] = EstadoPallet.Despachado,
        ["F"] = EstadoPallet.EnFrio,
        ["P"] = EstadoPallet.PreFrio,
        ["R"] = EstadoPallet.Rearmado,
        ["B"] = EstadoPallet.Bajado
        };

    public static string ToDisplayName(this EstadoPallet estado) => estado switch
        {
        EstadoPallet.Despachado => "Despachado",
        EstadoPallet.EnFrio => "En Frío",
        EstadoPallet.PreFrio => "Pre Frío",
        EstadoPallet.Rearmado => "Rearmado",
        EstadoPallet.Bajado => "Bajado",
        _ => estado.ToString()
        };

    /// <summary>
    /// Traduce etiqueta legible o código legado al enum. Devuelve false para
    /// valores desconocidos (el caller decide qué hacer: hoy cae en el
    /// fallback de datos de ejemplo).
    /// </summary>
    public static bool TryParse(string? valor, out EstadoPallet estado)
        => PorEtiquetaOCodigo.TryGetValue(valor?.Trim() ?? string.Empty, out estado);
    }

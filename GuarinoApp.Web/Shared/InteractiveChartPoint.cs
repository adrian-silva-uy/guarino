namespace GuarinoApp.Web.Shared;

/// <summary>Punto de una serie del gráfico: etiqueta + valor por serie (null = sin dato).</summary>
public sealed class InteractiveChartPoint
    {
    public string Label { get; init; } = string.Empty;
    public Dictionary<string, double?> Values { get; init; } = new();

    /// <summary>Valor de la serie en este punto; null = sin dato (hueco en la línea).</summary>
    public double? GetValue(string seriesName) => Values.TryGetValue(seriesName, out var value) ? value : null;
    }

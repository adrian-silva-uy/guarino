namespace GuarinoApp.Web.Shared;

/// <summary>
/// Serie de datos para InteractiveChart. Antes se usaba MudBlazor.ChartSeries
/// directamente (vía "@using MudBlazor"), pero esa clase es de una librería
/// externa y no se le puede agregar la propiedad Color que necesitamos para
/// que cada serie mantenga su color sin depender de su posición dentro del
/// gráfico (ver InteractiveChart.razor).
/// </summary>
public class ChartSeriesItem
    {
    public string Name { get; set; } = string.Empty;
    public double[] Data { get; set; } = Array.Empty<double>();

    public string[] Labels { get; set; } = Array.Empty<string>();

    /// <summary>Color explícito en formato hex (ej. "#008FFB"). Si es null, ApexCharts usa su paleta automática.</summary>
    public string? Color { get; set; }
    }

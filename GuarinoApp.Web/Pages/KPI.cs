using MudBlazor;

namespace GuarinoApp.Web.Pages;

/// <summary>Tarjeta de KPI del dashboard.</summary>
internal sealed class KPI
    {
    public string? Title { get; set; }
    public string? Value { get; set; }
    public string? PeriodLabel { get; set; }
    public string? TrendText { get; set; }
    public Color TrendColor { get; set; }
    public string? TrendIcon { get; set; }
    }

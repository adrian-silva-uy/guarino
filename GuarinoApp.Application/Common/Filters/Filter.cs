namespace GuarinoApp.Application.Common.Filters;
public class Filter
    {
    public string Field { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty; // eq, contains, gt, lt, between
    public string? Value { get; set; }
    public string? ValueTo { get; set; }
    public string Logic { get; set; } = "AND";
    }

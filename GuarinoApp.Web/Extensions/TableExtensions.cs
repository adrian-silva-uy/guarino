using MudBlazor;
using System.Text;

namespace GuarinoApp.Web.Extensions;
public static class TableExtensions
    {
    public static string ExportToCsv<T>(this MudTable<T> table, IEnumerable<T> items)
        {
        var properties = typeof(T).GetProperties();
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", properties.Select(p => p.Name)));
        foreach (var item in items)
            {
            var values = properties.Select(p => p.GetValue(item)?.ToString() ?? "");
            sb.AppendLine(string.Join(",", values));
            }
        return sb.ToString();
        }

    public static byte[] ExportToPdf<T>(this MudTable<T> table, IEnumerable<T> items, string title)
        {
        // Implementación simplificada - en producción usar iTextSharp o QuestPDF
        var html = $@"
        <html><head><meta charset='utf-8'><title>{title}</title></head>
        <body><h1>{title}</h1><table border='1'><tr>";
        var props = typeof(T).GetProperties();
        foreach (var p in props) html += $"<th>{p.Name}</th>";
        html += "</tr>";
        foreach (var item in items)
            {
            html += "<tr>";
            foreach (var p in props)
                html += $"<td>{p.GetValue(item)}</td>";
            html += "</tr>";
            }
        html += "</table></body></html>";
        return System.Text.Encoding.UTF8.GetBytes(html);
        }
    }

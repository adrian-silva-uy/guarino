using GuarinoApp.Application.Common.Interfaces;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace GuarinoApp.Infrastructure.Services.Skills;

/// <summary>
/// Skill de Stock: responde con datos reales de la planta — bins por variedad,
/// total y cuál variedad tiene más cantidad. Si la consulta menciona una
/// variedad puntual (recibe el mensaje vía <see cref="ISkillConEntrada"/>),
/// destaca la cifra de esa variedad.
/// </summary>
public sealed class StockPlantaSkill : ISkillConEntrada
{
    private readonly IDashboardDataService _dashboard;

    public StockPlantaSkill(IDashboardDataService dashboard) => _dashboard = dashboard;

    public string Name => "stock_planta";

    public string Description =>
        "Stock actual en planta por variedad: bins por variedad, total y cuál variedad tiene más cantidad (destaca la variedad si la consulta la menciona).";

    public IReadOnlyCollection<string> Ejemplos => new[]
        {
        "¿Qué variedad tenemos más cantidad en planta?",
        "¿Cuál es el stock por variedad?"
        };

    public IReadOnlyCollection<string> Keywords => new[]
    {
        "stock", "variedad", "variedades", "bins", "planta", "inventario",
        "mas cantidad", "mayor cantidad", "que variedad", "cual variedad",
        "deposito", "depositos", "en planta"
    };

    public Task<SkillOutput> ExecuteAsync(CancellationToken cancellationToken = default)
        => ExecuteAsync(string.Empty, cancellationToken);

    public async Task<SkillOutput> ExecuteAsync(string entrada, CancellationToken cancellationToken = default)
    {
        var porVariedad = await _dashboard.GetStockByVarietyAsync(null);
        if (porVariedad is not { Count: > 0 })
            return SkillOutput.Error("No hay datos de stock por variedad en planta todavía.");

        var ordenado = porVariedad.OrderByDescending(kv => kv.Value).ToList();
        var total = ordenado.Sum(kv => (long)kv.Value);
        var ultimaFecha = await _dashboard.GetLastStockDateAsync();

        var sb = new StringBuilder();
        sb.Append(ultimaFecha.HasValue
            ? $"Stock en planta (corte al {ultimaFecha:yyyy-MM-dd}): total {Fmt(total)} bins."
            : $"Stock en planta: total {Fmt(total)} bins.");
        sb.AppendLine();
        sb.AppendLine("Bins por variedad:");
        foreach (var kv in ordenado.Take(12))
            sb.AppendLine($"- {kv.Key}: {Fmt(kv.Value)}");
        if (ordenado.Count > 12)
            sb.AppendLine($"(y {ordenado.Count - 12} variedades más con menor stock)");

        var top = ordenado.First();
        var participacion = total > 0 ? (top.Value * 100m / total).ToString("0.0", CultureInfo.InvariantCulture) : "0.0";
        sb.AppendLine($"Variedad con más cantidad: {top.Key} ({Fmt(top.Value)} bins, {participacion}% del total).");

        var mencionada = HallarVariedad(entrada, ordenado.Select(kv => kv.Key));
        if (mencionada != null)
            sb.AppendLine($"La consulta menciona {mencionada}: {Fmt(porVariedad[mencionada])} bins en planta.");

        return SkillOutput.Ok(sb.ToString().TrimEnd());
    }

    private static string Fmt(decimal valor) => valor.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>
    /// Busca en el mensaje una variedad que exista en el stock (comparación sin
    /// acentos ni mayúsculas y como palabra completa). Devuelve la clave original
    /// del stock o null si el usuario no mencionó ninguna variedad conocida.
    /// </summary>
    private static string? HallarVariedad(string? entrada, IEnumerable<string> variedades)
    {
        if (string.IsNullOrWhiteSpace(entrada))
            return null;

        var mensaje = Normalizar(entrada);
        foreach (var variedad in variedades)
        {
            var clave = Normalizar(variedad);
            if (clave.Length > 0 && Regex.IsMatch(mensaje, $@"\b{Regex.Escape(clave)}\b", RegexOptions.IgnoreCase))
                return variedad;
        }

        return null;
    }

    /// <summary>Normaliza a minúsculas y sin acentos (misma receta que SkillRegistry).</summary>
    private static string Normalizar(string texto)
    {
        var descompuesto = texto.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(descompuesto.Length);
        foreach (var ch in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                builder.Append(ch);
        }
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}

using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.LotesVolcados.Queries.GetLotesVolcados;
using MediatR;
using System.Globalization;
using System.Text;

namespace GuarinoApp.Infrastructure.Services.Skills;

/// <summary>
/// Skill de Lotes/Volcados: totales de los últimos 60 días (kilos y bins
/// volcados), reparto por variedad y los lotes más recientes volcados.
/// Nunca alimenta al modelo con el fallback dummy del handler (EsDummy).
/// </summary>
public sealed class VolcadosLotesSkill : ISkill
{
    private readonly IMediator _mediator;

    public VolcadosLotesSkill(IMediator mediator) => _mediator = mediator;

    public string Name => "volcados_lotes";

    public string Description =>
        "Volcados de fruta de los últimos 60 días: kilos y bins volcados, reparto por variedad y lotes recientes con productor.";

    public IReadOnlyCollection<string> Ejemplos => new[]
        {
        "¿Cuánta fruta se volcó y de qué lotes?"
        };

    public IReadOnlyCollection<string> Keywords => new[]
    {
        "volcado", "volcados", "volcada", "volcadas", "volcar", "volcaron",
        "lote", "lotes", "emboquille"
    };

    public async Task<SkillOutput> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var desde = DateTime.Today.AddDays(-60);
        var resultado = await _mediator.Send(new GetLotesVolcadosQuery(FechaDesde: desde), cancellationToken);

        if (resultado == null || resultado.EsDummy)
            return SkillOutput.Error("No hay volcados reales registrados: la planta no tiene datos de lotes conectados.");

        if (resultado.Lotes is not { Count: > 0 })
            return SkillOutput.Ok($"No hay lotes volcados desde el {desde:yyyy-MM-dd} (sin volcados en los últimos 60 días).");

        var lotes = resultado.Lotes;
        var totalKilos = lotes.Sum(l => l.KilosVolcados);
        var totalBins = lotes.Sum(l => l.BinsVolcados);

        var sb = new StringBuilder();
        sb.AppendLine($"Volcados desde {desde:yyyy-MM-dd}: {lotes.Count} lotes, " +
                      $"{totalKilos.ToString("N0", CultureInfo.InvariantCulture)} kg, " +
                      $"{totalBins.ToString("N0", CultureInfo.InvariantCulture)} bins.");

        sb.AppendLine("Por variedad:");
        foreach (var grupo in lotes
            .GroupBy(l => string.IsNullOrWhiteSpace(l.Variedad) ? "Sin variedad" : l.Variedad)
            .OrderByDescending(g => g.Sum(l => l.KilosVolcados))
            .Take(8))
        {
            sb.AppendLine($"- {grupo.Key}: {grupo.Sum(l => l.KilosVolcados).ToString("N0", CultureInfo.InvariantCulture)} kg " +
                          $"en {grupo.Count()} lote(s)");
        }

        sb.AppendLine("Lotes más recientes:");
        foreach (var l in lotes.OrderByDescending(l => l.FechaVolcado).Take(8))
        {
            sb.AppendLine($"- {l.Codigo} · {l.Variedad} · {l.Productor} · {l.FechaVolcado:yyyy-MM-dd} · " +
                          $"{l.KilosVolcados.ToString("N0", CultureInfo.InvariantCulture)} kg ({l.BinsVolcados} bins)");
        }

        return SkillOutput.Ok(sb.ToString().TrimEnd());
    }
}

using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.RegistroPesadas.Queries.GetRegistroPesadas;
using MediatR;
using System.Globalization;
using System.Text;

namespace GuarinoApp.Infrastructure.Services.Skills;

/// <summary>
/// Skill de Registro de Pesadas: totales de los últimos 30 días (pesadas, kg,
/// bins), reparto por báscula y detalle de las pesadas más recientes.
/// Nunca alimenta al modelo con el fallback dummy del handler (EsDummy).
/// </summary>
public sealed class PesadasSkill : ISkill
{
    private readonly IMediator _mediator;

    public PesadasSkill(IMediator mediator) => _mediator = mediator;

    public string Name => "pesadas_recientes";

    public string Description =>
        "Pesadas de los últimos 30 días: cantidad de pesadas, kilos y bins totales, reparto por báscula y últimas pesadas con lote y productor.";

    public IReadOnlyCollection<string> Ejemplos => new[]
        {
        "¿Cuántas pesadas tuvimos en los últimos días y de qué lotes?"
        };

    public IReadOnlyCollection<string> Keywords => new[]
    {
        "pesada", "pesadas", "pesaje", "pesajes", "balanza", "balanzas",
        "bascula", "basculas", "pesamos", "pesado", "pesados"
    };

    public async Task<SkillOutput> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var desde = DateTime.Today.AddDays(-30);
        var resultado = await _mediator.Send(new GetRegistroPesadasQuery(FechaDesde: desde), cancellationToken);

        if (resultado == null || resultado.EsDummy)
            return SkillOutput.Error("No hay pesadas reales registradas: la planta no tiene datos de pesada conectados.");

        if (resultado.Pesadas is not { Count: > 0 })
            return SkillOutput.Ok($"No hay pesadas registradas desde el {desde:yyyy-MM-dd} (sin movimiento de balanza en los últimos 30 días).");

        var pesadas = resultado.Pesadas;
        var totalKilos = pesadas.Sum(p => p.Peso);
        var totalBins = pesadas.Sum(p => p.Bins);

        var sb = new StringBuilder();
        sb.AppendLine($"Pesadas desde {desde:yyyy-MM-dd}: {pesadas.Count} pesadas, " +
                      $"{totalKilos.ToString("N0", CultureInfo.InvariantCulture)} kg, " +
                      $"{totalBins.ToString("N0", CultureInfo.InvariantCulture)} bins.");

        sb.AppendLine("Por báscula:");
        foreach (var grupo in pesadas
            .GroupBy(p => string.IsNullOrWhiteSpace(p.Bascula) ? "Sin asignar" : p.Bascula)
            .OrderByDescending(g => g.Sum(p => p.Peso))
            .Take(5))
        {
            sb.AppendLine($"- {grupo.Key}: {grupo.Count()} pesadas, {grupo.Sum(p => p.Peso).ToString("N0", CultureInfo.InvariantCulture)} kg");
        }

        sb.AppendLine("Últimas pesadas:");
        foreach (var p in pesadas.OrderByDescending(p => p.Fecha).Take(8))
        {
            sb.AppendLine($"- {p.Fecha:yyyy-MM-dd HH:mm} · lote {p.Lote} · {p.Productor} · " +
                          $"{p.Peso.ToString("N0", CultureInfo.InvariantCulture)} kg ({p.Bins} bins) · {p.Bascula}");
        }

        return SkillOutput.Ok(sb.ToString().TrimEnd());
    }
}

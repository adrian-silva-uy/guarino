using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.ResumenLiquidaciones.Queries.GetResumenLiquidaciones;
using MediatR;
using System.Globalization;

namespace GuarinoApp.Infrastructure.Services.Skills;

/// <summary>
/// Skill de Reportes: resume las liquidaciones por productor
/// (totales a pagar, kilos y bins).
/// </summary>
public sealed class ResumenLiquidacionesSkill : ISkill
{
    private readonly IMediator _mediator;

    public ResumenLiquidacionesSkill(IMediator mediator) => _mediator = mediator;

    public string Name => "resumen_liquidaciones";

    public string Description =>
        "Resumen de liquidaciones: total a pagar, kilos y bins por productor y variedad.";

    public IReadOnlyCollection<string> Keywords => new[]
    {
        "liquidacion", "liquidaciones", "quinta", "quintas",
        "lo que se le debe", "total a pagar", "pagos a productores"
    };

    public async Task<SkillOutput> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var resultado = await _mediator.Send(new GetResumenLiquidacionesQuery(), cancellationToken);
        var liquidaciones = resultado?.Liquidaciones ?? [];

        if (liquidaciones == null || liquidaciones.Count == 0)
            return SkillOutput.Error("No hay liquidaciones registradas en el sistema.");

        var totalPagar = liquidaciones.Sum(l => l.TotalPagar);
        var totalKgs = liquidaciones.Sum(l => l.TotalKgs);
        var totalBins = liquidaciones.Sum(l => l.TotalBins);

        var detalle = liquidaciones
            .OrderByDescending(l => l.TotalPagar)
            .Take(10)
            .Select(l =>
                $"- {l.Productor ?? "Sin productor"} ({l.Variedad ?? "Sin variedad"}, {l.Año}): " +
                $"{l.TotalBins.ToString("N0", CultureInfo.InvariantCulture)} bins, " +
                $"{l.TotalKgs.ToString("N0", CultureInfo.InvariantCulture)} kg, " +
                $"a pagar {l.TotalPagar.ToString("N2", CultureInfo.InvariantCulture)}")
            .ToList();

        var encabezado =
            $"Resumen de liquidaciones ({liquidaciones.Count} registros):\n" +
            $"- Total a pagar: {totalPagar.ToString("N2", CultureInfo.InvariantCulture)}\n" +
            $"- Total de fruta: {totalKgs.ToString("N0", CultureInfo.InvariantCulture)} kg en {totalBins.ToString("N0", CultureInfo.InvariantCulture)} bins";

        var pie = liquidaciones.Count > 10
            ? "\nDetalle de los 10 productores de mayor importe:"
            : "\nDetalle por productor:";

        return SkillOutput.Ok(encabezado + pie + "\n" + string.Join("\n", detalle));
    }
}

using GuarinoApp.Application.Common;
using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.RendimientoPacking.Dtos;
using GuarinoApp.Application.Modules.RendimientoPacking.Queries.GetRendimientoPacking;
using MediatR;
using System.Globalization;

namespace GuarinoApp.Infrastructure.Services.Skills;

/// <summary>
/// Skill de rendimiento de packing: kg volcados vs exportable, mercado interno
/// y descarte, con porcentajes por variedad y productor. Reservado a Admin,
/// Gerencia y Contador.
/// </summary>
public sealed class RendimientoPackingSkill : ISkill
{
    private readonly IMediator _mediator;

    public RendimientoPackingSkill(IMediator mediator) => _mediator = mediator;

    public string Name => "rendimiento_packing";

    public string Description =>
        "Rendimiento de packing: de cada 100 kg volcados, cuánto termina en " +
        "exportación, mercado interno y descarte, con porcentajes por variedad " +
        "y productor. Reservado a Admin, Gerencia y Contador.";

    public IReadOnlyCollection<string> Ejemplos => new[]
    {
        "¿Cuál es el rendimiento de packing: exportable, mercado interno y descarte?",
        "¿Cuántos kg de cada 100 volcados se exportan?"
    };

    public IReadOnlyCollection<string> Keywords => new[]
    {
        "packing", "rendimiento", "exportable", "mercado interno", "descarte",
        "packout", "calibradora", "kg exportable", "mercado interno", "descarte",
        "porcentaje", "variedad", "productor"
    };

    public IReadOnlyCollection<string> RolesPermitidos => RoleNames.Financieros;

    public async Task<SkillOutput> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var resultado = await _mediator.Send(new GetRendimientoPackingQuery(), cancellationToken);

        if (!resultado.Detalle.Any())
            return SkillOutput.Error("No hay datos de rendimiento de packing disponibles.");

        var lineas = new List<string>
        {
            $"TOTAL: {resultado.TotalKgVolcados.ToString("N0", CultureInfo.InvariantCulture)} kg volcados",
            $"  Exportable: {resultado.TotalKgExportable.ToString("N0", CultureInfo.InvariantCulture)} kg ({resultado.PctTotalExportable.ToString("N1", CultureInfo.InvariantCulture)}%)",
            $"  Mercado interno: {resultado.TotalKgMercadoInterno.ToString("N0", CultureInfo.InvariantCulture)} kg ({resultado.PctTotalMercadoInterno.ToString("N1", CultureInfo.InvariantCulture)}%)",
            $"  Descarte: {resultado.TotalKgDescarte.ToString("N0", CultureInfo.InvariantCulture)} kg ({resultado.PctTotalDescarte.ToString("N1", CultureInfo.InvariantCulture)}%)"
        };

        foreach (var d in resultado.Detalle.Take(10))
        {
            lineas.Add(
                $"{d.Variedad} / {d.Productor}: {d.KgVolcados.ToString("N0")} kg volcados → " +
                $"export {d.PctExportable.ToString("N1")}%, interno {d.PctMercadoInterno.ToString("N1")}%, descarte {d.PctDescarte.ToString("N1")}%");
        }

        var encabezado = $"Rendimiento de packing (última zafra con datos):\n";
        return SkillOutput.Ok(encabezado + string.Join("\n", lineas));
    }
}

using GuarinoApp.Application.Common;
using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.Proyeccion.Dtos;
using GuarinoApp.Application.Modules.Proyeccion.Queries.GetComparativaZafraSemanal;
using MediatR;
using System.Globalization;

namespace GuarinoApp.Infrastructure.Services.Skills;

/// <summary>
/// Skill de comparativa interzafra semanal: curva acumulada de volcados y
/// pallets despachados por semana, comparando zafras. Reservado a Admin,
/// Gerencia y Contador.
/// </summary>
public sealed class ComparativaZafraSkill : ISkill
{
    private readonly IMediator _mediator;

    public ComparativaZafraSkill(IMediator mediator) => _mediator = mediator;

    public string Name => "comparativa_zafra";

    public string Description =>
        "Comparativa contra la zafra anterior en la misma fecha: curva acumulada " +
        "semanal de volcados y pallets despachados por zafra. Reservado a Admin, " +
        "Gerencia y Contador.";

    public IReadOnlyCollection<string> Ejemplos => new[]
    {
        "¿Cómo vamos vs la zafra anterior en esta misma semana?",
        "¿Vamos adelantados o atrasados respecto al año pasado?"
    };

    public IReadOnlyCollection<string> Keywords => new[]
    {
        "zafra", "comparativa", "interzafra", "semana", "acumulado", "volcados",
        "pallets despachados", "adelantados", "atrasados", "curva", "misma fecha"
    };

    public IReadOnlyCollection<string> RolesPermitidos => RoleNames.Financieros;

    public async Task<SkillOutput> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var resultado = await _mediator.Send(new GetComparativaZafraSemanalQuery(), cancellationToken);

        if (!resultado.Datos.Any())
            return SkillOutput.Error("No hay datos históricos de volcados para comparar zafras.");

        var lineas = new List<string>();
        foreach (var zafra in resultado.Zafras)
        {
            var datosZafra = resultado.Datos.Where(d => d.Zafra == zafra).OrderBy(d => d.Semana).ToList();
            var total = datosZafra.Sum(d => d.KgVolcadosAcumulados);
            var ultimaSemana = datosZafra.LastOrDefault();
            lineas.Add(
                $"Zafra {zafra}: {total.ToString("N0", CultureInfo.InvariantCulture)} kg volcados acumulados " +
                $"(última semana registrada: {ultimaSemana?.Semana ?? 0}, {ultimaSemana?.KgVolcadosAcumulados.ToString("N0", CultureInfo.InvariantCulture)} kg)");
        }

        var encabezado = $"Comparativa interzafra (volcados acumulados por semana):\n";
        return SkillOutput.Ok(encabezado + string.Join("\n", lineas));
    }
}

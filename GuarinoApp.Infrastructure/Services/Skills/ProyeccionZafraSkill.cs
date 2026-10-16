using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.Proyeccion.Queries.GetProyeccionZafra;
using MediatR;
using System.Globalization;

namespace GuarinoApp.Infrastructure.Services.Skills;

/// <summary>
/// Skill de Proyección: devuelve el detalle de la zafra proyectada
/// (kilos, variación interanual e impacto operativo en bins/camiones/horas).
/// </summary>
public sealed class ProyeccionZafraSkill : ISkill
{
    private readonly IMediator _mediator;

    public ProyeccionZafraSkill(IMediator mediator) => _mediator = mediator;

    public string Name => "proyeccion_zafra";

    public string Description =>
        "Detalle de la proyección de la próxima zafra: kilos, variación vs. año anterior, bins, camiones y horas de empaque estimadas.";

    public IReadOnlyCollection<string> Ejemplos => new[]
        {
        "¿Cuánto proyecta la próxima zafra en kilos, bins y camiones?"
        };

    public IReadOnlyCollection<string> Keywords => new[]
    {
        "zafra", "proxima zafra", "zafra siguiente", "proyeccion de zafra",
        "bins estimados", "camiones estimados", "horas de empaque",
        "variacion interanual", "temporada"
    };

    public async Task<SkillOutput> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var zafras = await _mediator.Send(new GetProyeccionZafraQuery(), cancellationToken);

        var objetivo = zafras?.FirstOrDefault(z => z.Estado == "Proyectado") ?? zafras?.LastOrDefault();
        if (objetivo == null)
            return SkillOutput.Error("No hay proyección de zafra disponible.");

        var signo = objetivo.VariacionPorcentual >= 0 ? "+" : "";
        var etiqueta = objetivo.EsDummy ? " [dato simulado: sin conexión a planta]" : string.Empty;

        var lineas = new List<string>
        {
            $"- Zafra: {objetivo.Año}{etiqueta}",
            $"- Kilos proyectados: {objetivo.TotalKgs.ToString("N0", CultureInfo.InvariantCulture)} kg",
            $"- Variación vs. año anterior: {signo}{objetivo.VariacionPorcentual.ToString("0.0", CultureInfo.InvariantCulture)}%",
            $"- Bins estimados: {objetivo.BinsEstimados?.ToString("N0", CultureInfo.InvariantCulture) ?? "N/D"}",
            $"- Camiones estimados: {objetivo.CamionesEstimados?.ToString("N0", CultureInfo.InvariantCulture) ?? "N/D"}",
            $"- Horas de empaque estimadas: {objetivo.HorasEmpaqueEstimadas?.ToString("N0", CultureInfo.InvariantCulture) ?? "N/D"}",
            $"- Riesgo de capacidad: {(objetivo.RiesgoCapacidad ? "ALTO (la proyección supera la capacidad instalada)" : "sin riesgo")}"
        };

        if (objetivo.CapacidadMaximaHistoricaKgs > 0)
            lineas.Add($"- Capacidad máxima histórica: {objetivo.CapacidadMaximaHistoricaKgs?.ToString("N0", CultureInfo.InvariantCulture)} kg");

        return SkillOutput.Ok("Proyección de la próxima zafra:\n" + string.Join("\n", lineas));
    }
}

using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.Contaduria.Queries.GetFinancialData;
using MediatR;
using System.Globalization;

namespace GuarinoApp.Infrastructure.Services.Skills;

/// <summary>
/// Skill de Contaduría: devuelve los datos financieros vigentes
/// (costo por kg, flujo de caja, margen bruto, flete y capacidad instalada).
/// </summary>
public sealed class FinancialDataSkill : ISkill
{
    private readonly IMediator _mediator;

    public FinancialDataSkill(IMediator mediator) => _mediator = mediator;

    public string Name => "datos_financieros";

    public string Description =>
        "Datos financieros vigentes: costo por kg, flujo de caja, margen bruto, costo de flete y capacidad instalada.";

    public IReadOnlyCollection<string> Ejemplos => new[]
        {
        "¿Cómo viene el flujo de caja, el margen y el costo por kg?"
        };

    public IReadOnlyCollection<string> Keywords => new[]
    {
        "costo", "costo por kg", "margen", "flujo de caja", "flete",
        "capacidad instalada", "datos financieros", "financiero", "financiera",
        "contaduria", "efectivo", "liquidez", "capex", "presupuesto"
    };

    public async Task<SkillOutput> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var data = await _mediator.Send(new GetFinancialDataQuery(), cancellationToken);

        if (data == null)
            return SkillOutput.Error("No hay datos financieros cargados en Contaduría todavía.");

        var lineas = new List<string>
        {
            $"- Costo por kg: {data.CostoPorKg.ToString("N2", CultureInfo.InvariantCulture)}",
            $"- Flujo de caja: {data.FlujoCaja.ToString("N2", CultureInfo.InvariantCulture)}",
            $"- Margen bruto histórico: {data.MargenBruto.ToString("N2", CultureInfo.InvariantCulture)}%",
            $"- Capacidad instalada: {data.CapacidadInstalada.ToString("N0", CultureInfo.InvariantCulture)} kg por zafra"
        };

        if (data.CostoFlete.HasValue)
            lineas.Add($"- Costo de flete por contenedor: {data.CostoFlete.Value.ToString("N2", CultureInfo.InvariantCulture)}");

        if (!string.IsNullOrWhiteSpace(data.Observaciones))
            lineas.Add($"- Observaciones: {data.Observaciones}");

        var encabezado =
            $"Datos financieros vigentes (actualizados {data.FechaActualizacion:yyyy-MM-dd} por {data.ActualizadoPor ?? "usuario no identificado"}):";

        return SkillOutput.Ok(encabezado + "\n" + string.Join("\n", lineas));
    }
}

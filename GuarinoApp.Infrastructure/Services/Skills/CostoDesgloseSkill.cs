using GuarinoApp.Application.Common;
using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.CostoDesglose.Dtos;
using GuarinoApp.Application.Modules.CostoDesglose.Queries.GetCostoDesglose;
using MediatR;
using System.Globalization;

namespace GuarinoApp.Infrastructure.Services.Skills;

/// <summary>
/// Skill de desglose de costo por kg exportable: devuelve el desglose de rubros
/// (fruta en planta, packing, cartón, energía/frío, flete/puerto, otros) y el
/// costo unitario por kg exportable. Reservado a Admin, Gerencia y Contador.
/// </summary>
public sealed class CostoDesgloseSkill : ISkill
{
    private readonly IMediator _mediator;

    public CostoDesgloseSkill(IMediator mediator) => _mediator = mediator;

    public string Name => "costo_desglose_kg";

    public string Description =>
        "Desglose del costo real por kg exportable: fruta en planta, packing, " +
        "cartón y envases, energía y frío, flete y puerto, y otros, con el costo " +
        "unitario por kg exportable. Reservado a Admin, Gerencia y Contador.";

    public IReadOnlyCollection<string> Ejemplos => new[]
    {
        "¿Cuál es el costo real por kg exportable desglosado por rubro?",
        "¿Cuánto cuesta dejar un kg listo para exportar?"
    };

    public IReadOnlyCollection<string> Keywords => new[]
    {
        "costo", "costo por kg", "costo real", "kg exportable", "desglose",
        "fruta en planta", "packing", "cartón", "envases", "energía", "frío",
        "flete", "puerto", "costo desglose", "costo unitario", "costo exportable"
    };

    public IReadOnlyCollection<string> RolesPermitidos => RoleNames.Financieros;

    public async Task<SkillOutput> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var data = await _mediator.Send(new GetCostoDesgloseQuery(), cancellationToken);

        if (data == null)
            return SkillOutput.Error("No hay desglose de costo por kg cargado. Registre el desglose en el módulo de Costo por kg (Contaduría/Gerencia).");

        var lineas = new List<string>
        {
            $"- Zafra: {data.Zafra}",
            $"- Fruta puesta en planta: ${data.CostoFrutaPlanta.ToString("N2", CultureInfo.InvariantCulture)}",
            $"- Packing (mano de obra): ${data.CostoPackingManoObra.ToString("N2", CultureInfo.InvariantCulture)}",
            $"- Cartón y envases: ${data.CostoCartonEnvases.ToString("N2", CultureInfo.InvariantCulture)}",
            $"- Energía y frío: ${data.CostoEnergiaFrio.ToString("N2", CultureInfo.InvariantCulture)}",
            $"- Flete y puerto: ${data.CostoFletePuerto.ToString("N2", CultureInfo.InvariantCulture)}",
            $"- Otros: ${data.CostoOtros.ToString("N2", CultureInfo.InvariantCulture)}",
            $"- Costo total: ${data.CostoTotal.ToString("N2", CultureInfo.InvariantCulture)}",
            $"- Kg exportables proyectados: {data.KgExportablesProyectados.ToString("N0", CultureInfo.InvariantCulture)} kg",
            $"- COSTO POR KG EXPORTABLE: ${data.CostoPorKgExportable.ToString("N2", CultureInfo.InvariantCulture)}"
        };

        var encabezado =
            $"Desglose de costo por kg exportable (zafra {data.Zafra}, cargado {data.FechaCarga:yyyy-MM-dd} por {data.CargadoPor ?? "usuario no identificado"}):";

        if (!string.IsNullOrWhiteSpace(data.Observaciones))
            lineas.Add($"- Observaciones: {data.Observaciones}");

        return SkillOutput.Ok(encabezado + "\n" + string.Join("\n", lineas));
    }
}

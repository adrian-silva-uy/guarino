using GuarinoApp.Application.Common;
using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.Contaduria.Dtos;
using GuarinoApp.Application.Modules.Contaduria.Queries.GetProyeccionCaja;
using MediatR;
using System.Globalization;

namespace GuarinoApp.Infrastructure.Services.Skills;

/// <summary>
/// Skill de proyección de caja a 13 semanas: entradas y salidas por escenario
/// (Base, Pesimista, Optimista) y saldo acumulado. Reservado a Admin,
/// Gerencia y Contador.
/// </summary>
public sealed class ProyeccionCajaSkill : ISkill
{
    private readonly IMediator _mediator;

    public ProyeccionCajaSkill(IMediator mediator) => _mediator = mediator;

    public string Name => "proyeccion_caja";

    public string Description =>
        "Proyección de caja a 13 semanas: entradas (cobros por embarque), " +
        "salidas (liquidaciones a quintas, jornales, fletes, impuestos) y saldo " +
        "acumulado por escenario Base, Pesimista y Optimista. Reservado a Admin, " +
        "Gerencia y Contador.";

    public IReadOnlyCollection<string> Ejemplos => new[]
    {
        "¿Cómo viene la proyección de caja a 13 semanas?",
        "¿En qué semana se toca el piso de caja?",
        "¿Cuál es el escenario pesimista de caja?"
    };

    public IReadOnlyCollection<string> Keywords => new[]
    {
        "caja", "proyección de caja", "flujo de caja", "13 semanas", "semana",
        "piso de caja", "escenario", "base", "pesimista", "optimista",
        "entradas", "salidas", "saldo acumulado", "liquidez"
    };

    public IReadOnlyCollection<string> RolesPermitidos => RoleNames.Financieros;

    public async Task<SkillOutput> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var escenarios = new[] { "Base", "Pesimista", "Optimista" };
        var lineas = new List<string>();

        foreach (var escenario in escenarios)
        {
            var resultado = await _mediator.Send(new GetProyeccionCajaQuery(escenario), cancellationToken);

            if (!resultado.Datos.Any())
            {
                lineas.Add($"{escenario}: sin datos de proyección disponibles.");
                continue;
            }

            var piso = resultado.Datos.OrderBy(d => d.SaldoAcumuladoUsd).First();
            var semanaPiso = resultado.Datos.IndexOf(piso) + 1;

            lineas.Add(
                $"{escenario}: saldo inicial ${resultado.SaldoInicialUsd.ToString("N0", CultureInfo.InvariantCulture)}, " +
                $"mínimo ${piso.SaldoAcumuladoUsd.ToString("N0", CultureInfo.InvariantCulture)} en la semana {semanaPiso}");
        }

        var encabezado = "Proyección de caja a 13 semanas por escenario:\n";
        return SkillOutput.Ok(encabezado + string.Join("\n", lineas));
    }
}

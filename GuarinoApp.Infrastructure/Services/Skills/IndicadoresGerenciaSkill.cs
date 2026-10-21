using GuarinoApp.Application.Common;
using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.Gerencia.Queries.GetIndicadoresGerencia;
using MediatR;
using System.Globalization;
using System.Text;

namespace GuarinoApp.Infrastructure.Services.Skills;

/// <summary>
/// Skill de Panel de Gerencia: indicadores vigentes agrupados por categoría
/// con su valor y fecha de medición.
/// </summary>
public sealed class IndicadoresGerenciaSkill : ISkill
{
    private readonly IMediator _mediator;

    public IndicadoresGerenciaSkill(IMediator mediator) => _mediator = mediator;

    public string Name => "indicadores_gerencia";

    public string Description =>
        "Indicadores del Panel de Gerencia: valores vigentes agrupados por categoría con su fecha de medición.";

    public IReadOnlyCollection<string> Ejemplos => new[]
        {
        "¿Cuáles son los indicadores de gerencia actuales?"
        };

    public IReadOnlyCollection<string> Keywords => new[]
    {
        "indicador", "indicadores", "gerencia", "kpi", "kpis"
    };

    // Panel de Gerencia: restringido a los roles de administración (matriz de módulos).
    public IReadOnlyCollection<string> RolesPermitidos => RoleNames.Administracion;

    public async Task<SkillOutput> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var indicadores = await _mediator.Send(new GetIndicadoresGerenciaQuery(), cancellationToken);

        if (indicadores is not { Count: > 0 })
            return SkillOutput.Ok("No hay indicadores de gerencia cargados todavía.");

        var sb = new StringBuilder();
        sb.AppendLine($"Indicadores de gerencia: {indicadores.Count} en total.");

        foreach (var grupo in indicadores.GroupBy(i => string.IsNullOrWhiteSpace(i.Categoria) ? "Sin categoría" : i.Categoria).OrderBy(g => g.Key))
        {
            sb.AppendLine($"Categoría {grupo.Key}:");
            foreach (var i in grupo.OrderByDescending(i => i.Valor))
            {
                var fecha = i.Fecha.HasValue ? $" (al {i.Fecha:yyyy-MM-dd})" : string.Empty;
                sb.AppendLine($"- {i.Indicador}: {i.Valor.ToString("N2", CultureInfo.InvariantCulture)}{fecha}");
            }
        }

        return SkillOutput.Ok(sb.ToString().TrimEnd());
    }
}

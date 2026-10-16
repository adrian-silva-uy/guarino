using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.Auditoria.Queries.GetLogs;
using MediatR;

namespace GuarinoApp.Infrastructure.Services.Skills;

/// <summary>
/// Skill de Auditoría: devuelve los últimos registros de actividad
/// (quién hizo qué y cuándo).
/// </summary>
public sealed class ActividadAuditoriaSkill : ISkill
{
    private const int RegistrosAMostrar = 10;

    private readonly IMediator _mediator;

    public ActividadAuditoriaSkill(IMediator mediator) => _mediator = mediator;

    public string Name => "actividad_usuarios";

    public string Description =>
        "Últimos registros de actividad del sistema: usuario, acción, módulo y fecha/hora.";

    public IReadOnlyCollection<string> Ejemplos => new[]
        {
        "¿Quién hizo qué en el sistema últimamente?"
        };

    public IReadOnlyCollection<string> Keywords => new[]
    {
        "auditoria", "actividad", "log", "historial",
        "quien hizo", "usuarios que", "acciones recientes", "ultimo acceso"
    };

    public async Task<SkillOutput> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var logs = await _mediator.Send(
            new GetLogsQuery(Page: 1, PageSize: RegistrosAMostrar),
            cancellationToken);

        if (logs == null || logs.Items.Count == 0)
            return SkillOutput.Error("No hay actividad registrada en el log de auditoría todavía.");

        var detalle = logs.Items
            .Select(l =>
                $"- {l.Timestamp:yyyy-MM-dd HH:mm} | {l.UserName ?? "usuario no identificado"}" +
                $"{(string.IsNullOrWhiteSpace(l.Role) ? string.Empty : $" ({l.Role})")} | " +
                $"{l.Action} en {l.Module}: {l.Description}")
            .ToList();

        var encabezado =
            $"Últimos {logs.Items.Count} registros de actividad " +
            $"(total en el log: {logs.TotalCount}):";

        return SkillOutput.Ok(encabezado + "\n" + string.Join("\n", detalle));
    }
}

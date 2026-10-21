using GuarinoApp.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace GuarinoApp.Infrastructure.Services.Skills;

/// <summary>
/// Registro de skills con enrutamiento por intención basado en palabras clave
/// (sin depender de tool-calling del modelo, que con modelos locales pequeños
/// es poco confiable). Cada ejecución queda registrada en el log de auditoría.
///
/// Capa 2 del filtro de rol (defensa en profundidad): si la skill que ganó la
/// intención está reservada para otros roles según la matriz de módulos y el
/// usuario no pertenece, no se ejecuta — se responde con el rechazo
/// inteligente (mismo mensaje que la capa 1) auditado como
/// "ConsultaIArestringida". Cubre cualquier entrada que llegue al registro
/// aunque el filtro previo no haya clasificado la consulta.
/// </summary>
public sealed class SkillRegistry : ISkillRegistry
{
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<SkillRegistry> _logger;

    public SkillRegistry(
        IEnumerable<ISkill> skills,
        IAuditService auditService,
        ICurrentUserService currentUser,
        ILogger<SkillRegistry> logger)
    {
        Skills = skills.ToList();
        _auditService = auditService;
        _currentUser = currentUser;
        _logger = logger;
    }

    public IReadOnlyList<ISkill> Skills { get; }

    public async Task<SkillMatch?> MatchAsync(string userMessage, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
            return null;

        var normalizedMessage = TextoIA.Normalizar(userMessage);

        // La skill con más palabras clave coincidentes gana; ante empate,
        // gana la registrada primero (orden de prioridad).
        var best = Skills
            .Select((skill, index) => new { Skill = skill, Index = index, Score = TextoIA.ContarCoincidencias(normalizedMessage, skill.Keywords) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Index)
            .FirstOrDefault();

        if (best == null)
            return null;

        // Capa 2 del filtro por rol: la skill ganadora puede estar reservada
        // (p. ej. datos financieros para Admin/Gerencia/Contador). No se
        // ejecuta ni se le pasa un byte de datos al modelo.
        var rolActual = MensajeRechazoRol.ResolverRol(_currentUser);
        if (!MensajeRechazoRol.RolPermitido(best.Skill.RolesPermitidos, rolActual))
            {
            var mensaje = MensajeRechazoRol.Construir(
                MensajeRechazoRol.TemaCorto(best.Skill.Description),
                best.Skill.RolesPermitidos,
                rolActual,
                Skills);
            await AuditRestringidaAsync(best.Skill, rolActual, userMessage);
            _logger.LogInformation(
                "SkillRegistry: skill '{Skill}' no ejecutada por rol {Rol} (consulta restringida).",
                best.Skill.Name, rolActual ?? "sin rol");
            return new SkillMatch(
                best.Skill.Name,
                Success: false,
                Text: mensaje,
                Restringido: true);
            }

        try
        {
            // Las skills con entrada (p. ej. importar contactos) reciben el
            // mensaje del usuario; las de consulta siguen siendo ciegas.
            var output = best.Skill is ISkillConEntrada conEntrada
                ? await conEntrada.ExecuteAsync(userMessage, cancellationToken)
                : await best.Skill.ExecuteAsync(cancellationToken);
            await AuditAsync(best.Skill.Name, output.Success, output.Text);
            return new SkillMatch(best.Skill.Name, output.Success, output.Text);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ejecutando la skill {Skill}", best.Skill.Name);
            await AuditAsync(best.Skill.Name, success: false, detail: ex.GetType().Name);
            return new SkillMatch(
                best.Skill.Name,
                Success: false,
                Text: "No se pudo consultar la información solicitada en este momento.");
        }
    }

    private async Task AuditRestringidaAsync(ISkill skill, string? rolActual, string userMessage)
    {
        try
        {
            var recorte = userMessage.Length > 500 ? userMessage[..500] : userMessage;
            await _auditService.LogAsync(
                action: "ConsultaIArestringida",
                module: "Asistente IA",
                description:
                    $"Usuario '{_currentUser.UserName ?? "desconocido"}' ({rolActual ?? "sin rol"}) " +
                    $"intentó ejecutar la habilidad '{skill.Name}', reservada para " +
                    $"{string.Join(", ", skill.RolesPermitidos)}.",
                additionalData: $"Consulta: {recorte}");
        }
        catch (Exception ex)
        {
            // La auditoría nunca debe tumbar la consulta del usuario.
            _logger.LogWarning(ex, "No se pudo auditar la consulta restringida de la skill {Skill}", skill.Name);
        }
    }

    private async Task AuditAsync(string skillName, bool success, string detail = "")
    {
        try
        {
            var estado = success ? "con datos" : "sin datos";
            await _auditService.LogAsync(
                action: "Consulta IA",
                module: "Asistente IA",
                description: $"Ejecutó la habilidad '{skillName}' ({estado})",
                additionalData: success && detail.Length > 0 ? detail[..Math.Min(detail.Length, 500)] : null);
        }
        catch (Exception ex)
        {
            // La auditoría nunca debe tumbar la consulta del usuario.
            _logger.LogWarning(ex, "No se pudo auditar la ejecución de la skill {Skill}", skillName);
        }
    }
}

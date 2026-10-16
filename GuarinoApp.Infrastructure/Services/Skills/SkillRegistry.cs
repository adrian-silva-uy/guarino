using GuarinoApp.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace GuarinoApp.Infrastructure.Services.Skills;

/// <summary>
/// Registro de skills con enrutamiento por intención basado en palabras clave
/// (sin depender de tool-calling del modelo, que con modelos locales pequeños
/// es poco confiable). Cada ejecución queda registrada en el log de auditoría.
/// </summary>
public sealed class SkillRegistry : ISkillRegistry
{
    private readonly IAuditService _auditService;
    private readonly ILogger<SkillRegistry> _logger;

    public SkillRegistry(
        IEnumerable<ISkill> skills,
        IAuditService auditService,
        ILogger<SkillRegistry> logger)
    {
        Skills = skills.ToList();
        _auditService = auditService;
        _logger = logger;
    }

    public IReadOnlyList<ISkill> Skills { get; }

    public async Task<SkillMatch?> MatchAsync(string userMessage, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
            return null;

        var normalizedMessage = Normalize(userMessage);

        // La skill con más palabras clave coincidentes gana; ante empate,
        // gana la registrada primero (orden de prioridad).
        var best = Skills
            .Select((skill, index) => new { Skill = skill, Index = index, Score = Score(normalizedMessage, skill) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Index)
            .FirstOrDefault();

        if (best == null)
            return null;

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

    private static int Score(string normalizedMessage, ISkill skill)
        => skill.Keywords.Count(keyword =>
        {
            var normalizedKeyword = Normalize(keyword);
            if (normalizedKeyword.Length == 0)
                return false;

            // Palabra completa: "caja" no matchea "cajas", "log" no matchea "login".
            return Regex.IsMatch(
                normalizedMessage,
                $@"\b{Regex.Escape(normalizedKeyword)}\b",
                RegexOptions.IgnoreCase);
        });

    /// <summary>
    /// Normaliza texto a minúsculas y sin acentos para comparar palabras clave
    /// independientemente de la escritura del usuario.
    /// </summary>
    private static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                builder.Append(ch);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
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

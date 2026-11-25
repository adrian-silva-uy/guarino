using GuarinoApp.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace GuarinoApp.Infrastructure.Services;

/// <summary>
/// Guard de dominio y ranking de consultas del Asistente IA.
/// Las skills son la fuente de verdad de los temas que la aplicación conoce;
/// por eso una consulta nueva puede ser libre, pero debe mencionar al menos un
/// concepto de negocio reconocido por una skill antes de llegar al modelo.
/// </summary>
public sealed class ConsultaNegocioService : IBusinessQueryService
{
    private const string AcceptedQueryAction = "ConsultaIAAceptada";
    private const string AssistantModule = "Asistente IA";

    private readonly ISkillRegistry _skillRegistry;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;
    private readonly ILogger<ConsultaNegocioService> _logger;

    public ConsultaNegocioService(
        ISkillRegistry skillRegistry,
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IAuditService audit,
        ILogger<ConsultaNegocioService> logger)
    {
        _skillRegistry = skillRegistry;
        _context = context;
        _currentUser = currentUser;
        _audit = audit;
        _logger = logger;
    }

    public Task<BusinessQueryValidationResult> ValidateAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Task.FromResult(Rechazada());

        var normalized = TextoIA.Normalizar(query);
        var matchesBusinessTopic = _skillRegistry.Skills.Any(skill =>
            TextoIA.ContarCoincidencias(normalized, skill.Keywords) > 0);

        if (matchesBusinessTopic)
            return Task.FromResult(new BusinessQueryValidationResult(true));

        return Task.FromResult(Rechazada());
    }

    public async Task RegisterAcceptedAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var canonical = Canonicalize(query);
        if (canonical.Length == 0)
            return;

        try
        {
            await _audit.LogAsync(
                AcceptedQueryAction,
                AssistantModule,
                $"Consulta de negocio aceptada: \"{canonical}\"",
                canonical);
        }
        catch (Exception ex)
        {
            // El ranking nunca debe interrumpir una respuesta válida del asistente.
            _logger.LogWarning(ex, "No se pudo registrar la consulta frecuente del Asistente IA.");
        }
    }

    public async Task<IReadOnlyList<FrequentBusinessQuery>> GetFrequentAsync(
        int limit = 8,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0 || string.IsNullOrWhiteSpace(_currentUser.UserName))
            return Array.Empty<FrequentBusinessQuery>();

        try
        {
            var entries = await _context.LogEntries
                .AsNoTracking()
                .Where(x => x.Action == AcceptedQueryAction
                    && x.Module == AssistantModule
                    && x.UserName == _currentUser.UserName
                    && x.AdditionalData != null)
                .Select(x => x.AdditionalData!)
                .ToListAsync(cancellationToken);

            return entries
                .Select(Canonicalize)
                .Where(x => x.Length > 0)
                .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
                .Select(group => new FrequentBusinessQuery(group.First(), group.Count()))
                .OrderByDescending(x => x.UsageCount)
                .ThenBy(x => x.Text, StringComparer.OrdinalIgnoreCase)
                .Take(limit)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudieron cargar las consultas frecuentes del Asistente IA.");
            return Array.Empty<FrequentBusinessQuery>();
        }
    }

    private static BusinessQueryValidationResult Rechazada() => new(
        false,
        "Puedo ayudarte únicamente con información y procesos de Guarino. " +
        "Reformulá la consulta relacionándola con stock, empaque, logística, " +
        "producción, finanzas, contactos o algún otro dato del negocio.");

    private static string Canonicalize(string value)
        => Regex.Replace(value.Trim(), @"\s+", " ");
}

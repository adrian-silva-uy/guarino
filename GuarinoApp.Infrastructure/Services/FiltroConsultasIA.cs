namespace GuarinoApp.Infrastructure.Services;

using GuarinoApp.Application.Common;
using GuarinoApp.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

/// <summary>
/// Capa 1 del filtro de consultas IA por rol. Clasifica la pregunta con los
/// MISMOS keywords que el enrutador de skills (más categorías estáticas para
/// las páginas restringidas que no tienen skill propia: Facturación, Resumen
/// Mensual y gestión de Usuarios) y, si el mejor tema detectado está reservado
/// para otros roles según la matriz de módulos, devuelve el rechazo
/// inteligente y lo registra en Auditoría como "ConsultaIArestringida".
///
/// Se invoca en FallbackAIService.ChatAsync antes de la caché: un rechazo
/// nunca se cachea ni llega al modelo. Ante empate de score gana el tema
/// restringido (conservador: ante la duda se rechaza y se audita, jamás se
/// filtra información reservada). Si el filtro falla inesperadamente, deja
/// pasar la consulta: la capa 2 (guard de SkillRegistry) sigue protegiendo
/// los datos de las skills.
/// </summary>
public sealed class FiltroConsultasIA : IFiltroConsultasIA
{
    private readonly IReadOnlyList<ISkill> _skills;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;
    private readonly ILogger<FiltroConsultasIA> _logger;

    public FiltroConsultasIA(
        IEnumerable<ISkill> skills,
        ICurrentUserService currentUser,
        IAuditService audit,
        ILogger<FiltroConsultasIA> logger)
    {
        _skills = skills.ToList();
        _currentUser = currentUser;
        _audit = audit;
        _logger = logger;
    }

    /// <summary>Página restringida sin skill propia: tema, keywords y roles.</summary>
    private sealed record Categoria(string Tema, string[] Keywords, IReadOnlyCollection<string> Roles);

    /// <summary>
    /// Matriz de páginas restringidas que el enrutador de skills no cubre:
    /// Facturación y Resumen Mensual (financieros) y Usuarios (administración).
    /// Los nombres de rol vienen de RoleNames (fuente única de verdad).
    /// </summary>
    private static readonly Categoria[] CategoriasEstaticas =
        {
        new("Facturación",
            new[] { "facturacion", "factura", "facturas", "facturado", "lo facturado" },
            RoleNames.Financieros),
        new("Resumen mensual",
            new[] { "resumen mensual", "cierre mensual", "resumen del mes" },
            RoleNames.Financieros),
        new("Gestión de usuarios",
            new[] { "usuario", "usuarios", "rol", "roles", "permiso", "permisos",
                    "crear usuario", "baja de usuario", "alta de usuario" },
            RoleNames.Administracion)
        };

    public async Task<string?> EvaluarAsync(string pregunta, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pregunta))
            return null;

        try
        {
            var rolActual = MensajeRechazoRol.ResolverRol(_currentUser);
            var normalizada = TextoIA.Normalizar(pregunta);

            // Mejor tema: skills (restringidas u operativas) + categorías estáticas.
            // Las skills operativas compiten para que una consulta operativa más
            // específica (p. ej. "pallets sin cliente") gane al keyword flojo de
            // un tema restringido ("cliente"). Empate: gana el restringido.
            var candidatos = _skills
                .Select((skill, index) => new
                    {
                    Index = index,
                    Score = TextoIA.ContarCoincidencias(normalizada, skill.Keywords),
                    skill.Name,
                    Tema = MensajeRechazoRol.TemaCorto(skill.Description),
                    skill.RolesPermitidos
                    })
                .Concat(CategoriasEstaticas.Select((cat, i) => new
                    {
                    Index = _skills.Count + i,
                    Score = TextoIA.ContarCoincidencias(normalizada, cat.Keywords),
                    Name = cat.Tema,
                    cat.Tema,
                    RolesPermitidos = cat.Roles
                    }))
                .Where(c => c.Score > 0)
                .OrderByDescending(c => c.Score)
                .ThenByDescending(c => c.RolesPermitidos.Count > 0 ? 1 : 0)
                .ThenBy(c => c.Index)
                .FirstOrDefault();

            if (candidatos is null || MensajeRechazoRol.RolPermitido(candidatos.RolesPermitidos, rolActual))
                return null;

            var mensaje = MensajeRechazoRol.Construir(
                candidatos.Tema, candidatos.RolesPermitidos, rolActual, _skills);

            await AuditarAsync(candidatos.Name, candidatos.RolesPermitidos, rolActual, pregunta);
            _logger.LogInformation(
                "Filtro IA: consulta de {Usuario} ({Rol}) rechazada por tema restringido '{Tema}'.",
                _currentUser.UserName ?? "sin usuario", rolActual ?? "sin rol", candidatos.Name);

            return mensaje;
        }
        catch (Exception ex)
        {
            // El filtro nunca debe tumbar el chat: si falla, la consulta sigue
            // su camino y la capa 2 (SkillRegistry) protege los datos de skills.
            _logger.LogError(ex, "Filtro IA: evaluación fallida; se deja pasar la consulta.");
            return null;
        }
    }

    private async Task AuditarAsync(
        string tema, IReadOnlyCollection<string> rolesDelTema, string? rolActual, string pregunta)
    {
        try
        {
            var recorte = pregunta.Length > 500 ? pregunta[..500] : pregunta;
            await _audit.LogAsync(
                action: "ConsultaIArestringida",
                module: "Asistente IA",
                description:
                    $"Usuario '{_currentUser.UserName ?? "desconocido"}' ({rolActual ?? "sin rol"}) " +
                    $"intentó consultar el tema '{tema}', reservado para " +
                    $"{string.Join(", ", rolesDelTema)}.",
                additionalData: $"Consulta: {recorte}");
        }
        catch (Exception ex)
        {
            // La auditoría nunca debe tumbar la consulta del usuario.
            _logger.LogWarning(ex, "Filtro IA: no se pudo auditar el intento de consulta restringida.");
        }
    }
}

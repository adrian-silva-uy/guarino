namespace GuarinoApp.Infrastructure.Services;

using GuarinoApp.Application.Common;
using GuarinoApp.Application.Common.Interfaces;
using System.Text;

/// <summary>
/// Construye el rechazo inteligente cuando un rol intenta consultar un tema
/// restringido: nombra el tema detectado, indica qué roles sí pueden verlo y
/// sugiere temas permitidos para el perfil del usuario (derivados de las
/// skills reales, no inventados). Compuesto por la capa 1 (FiltroConsultasIA)
/// y la capa 2 (guard de SkillRegistry) para que el mensaje sea idéntico.
/// </summary>
internal static class MensajeRechazoRol
{
    /// <summary>Recortes de sugerencia: primeras letras del ejemplo de la skill.</summary>
    private const int MaximoSugerencias = 4;

    /// <param name="temaDetectado">Nombre legible del tema que disparó el rechazo
    /// (p. ej. "Datos financieros vigentes").</param>
    /// <param name="rolesDelTema">Roles que sí pueden consultar ese tema.</param>
    /// <param name="rolActual">Rol del usuario actual, o null si no tiene rol.</param>
    /// <param name="skills">Skills del registro, para sugerir temas permitidos.</param>
    internal static string Construir(
        string temaDetectado,
        IReadOnlyCollection<string> rolesDelTema,
        string? rolActual,
        IEnumerable<ISkill> skills)
    {
        var sb = new StringBuilder();
        sb.Append("No puedo resolver esa consulta con tu perfil")
          .Append(rolActual is null ? " (sin rol asignado)" : $" ({rolActual})")
          .Append(": el tema \"")
          .Append(temaDetectado)
          .Append("\" está reservado para ")
          .Append(ComaOXy(rolesDelTema))
          .Append('.');

        var permitidas = skills
            .Where(s => s.RolesPermitidos.Count == 0
                || (rolActual is not null && s.RolesPermitidos.Contains(rolActual)))
            .Select(PrimerEjemplo)
            .Where(e => e.Length > 0)
            .Take(MaximoSugerencias)
            .ToList();

        if (permitidas.Count > 0)
            {
            sb.Append(" Sí podés preguntarme sobre: ")
              .Append(string.Join("; ", permitidas))
              .Append('.');
            }

        sb.Append(" Tu intento quedó registrado en Auditoría del Sistema.");
        return sb.ToString();
    }

    /// <summary>Primer ejemplo legible de la skill (sin terminar en dos puntos).</summary>
    private static string PrimerEjemplo(ISkill skill)
        => skill.Ejemplos.FirstOrDefault()?.Trim().TrimEnd('.', ':') ?? string.Empty;

    /// <summary>"Admin", "Admin y Gerencia", "Admin, Gerencia y Contador".</summary>
    private static string ComaOXy(IReadOnlyCollection<string> roles)
    {
        var lista = roles.ToList();
        return lista.Count switch
            {
            0 => "ningún rol",
            1 => lista[0],
            _ => string.Join(", ", lista.Take(lista.Count - 1)) + " y " + lista[^1]
            };
    }

    /// <summary>Devuelve true si el rol actual puede consultar el tema (vacío = todos).</summary>
    internal static bool RolPermitido(IReadOnlyCollection<string> rolesPermitidos, string? rolActual)
        => rolesPermitidos.Count == 0
           || (rolActual is not null
               && rolesPermitidos.Contains(rolActual, StringComparer.Ordinal));

    /// <summary>
    /// Rótulo corto del tema para el mensaje de rechazo: corta la descripción
    /// de la skill en el primer ':' o '(' y, si sigue siendo larga, en el
    /// límite de palabras. Ej.: "Datos financieros vigentes: costo por kg..."
    /// → "Datos financieros vigentes".
    /// </summary>
    internal static string TemaCorto(string descripcion)
    {
        if (string.IsNullOrWhiteSpace(descripcion))
            return string.Empty;

        var corte = descripcion.IndexOfAny(new[] { ':', '(' });
        var tema = (corte > 0 ? descripcion[..corte] : descripcion).Trim().TrimEnd('.', ',');

        const int Maximo = 70;
        if (tema.Length <= Maximo)
            return tema;

        var limite = tema.LastIndexOf(' ', Maximo);
        return (limite > 20 ? tema[..limite] : tema[..Maximo]).Trim();
    }

    /// <summary>Resuelve el primer rol del usuario contra la lista canónica de RoleNames.</summary>
    internal static string? ResolverRol(ICurrentUserService currentUser)
        => RoleNames.Todos.FirstOrDefault(r => currentUser.IsInRole(r));
}

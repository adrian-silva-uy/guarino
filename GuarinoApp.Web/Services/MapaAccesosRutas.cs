using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace GuarinoApp.Web.Services;

/// <summary>
/// Mapa ruta → roles permitidos, derivado por reflexión de los atributos
/// [Authorize] de las páginas Razor del ensamblado Web. Esos atributos ya son
/// la matriz canónica de autorización (fuente única de verdad junto con
/// <see cref="Application.Common.RoleNames"/>): el menú lateral y la página Inicio consultan este
/// mapa para mostrar los módulos fuera del perfil del usuario como bloqueados
/// con candado y tooltip explicando qué rol los requiere.
///
/// IMPORTANTE: este mapa es SOLO presentación (UX). La seguridad real la sigue
/// imponiendo cada página con su propio atributo [Authorize], que el framework
/// evalúa en cada navegación; si el mapa y un atributo divergen, siempre gana
/// el atributo.
/// </summary>
public sealed class MapaAccesosRutas
{
    // null  => página sin [Authorize] (anónima: p. ej. Login) → siempre permitida.
    // vacío => [Authorize] sin Roles (basta estar autenticado).
    // con   => [Authorize(Roles = ...): requiere alguno de esos roles.
    private readonly Dictionary<string, HashSet<string>?> _rolesPorRuta =
        new(StringComparer.OrdinalIgnoreCase);

    public MapaAccesosRutas()
    {
        foreach (var tipo in typeof(MapaAccesosRutas).Assembly.GetTypes())
        {
            var rutas = tipo.GetCustomAttributes<RouteAttribute>(inherit: false)
                .Select(r => r.Template)
                .ToArray();
            if (rutas.Length == 0)
                continue;

            var authorize = tipo.GetCustomAttribute<AuthorizeAttribute>(inherit: true);
            HashSet<string>? roles = null;
            if (authorize is not null)
            {
                roles = string.IsNullOrWhiteSpace(authorize.Roles)
                    ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    : authorize.Roles
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }

            foreach (var ruta in rutas)
                _rolesPorRuta[ruta] = roles;
        }
    }

    /// <summary>
    /// Indica si el rol actual (resuelto por la UI) puede abrir la ruta según
    /// su atributo [Authorize]. Rutas desconocidas se permiten (fail-open de
    /// presentación): la propia página bloqueará si corresponde.
    /// </summary>
    public bool Permite(string ruta, string? rolActual)
    {
        if (!_rolesPorRuta.TryGetValue(ruta, out var roles))
            return true;                       // ruta no registrada → no decide el mapa
        if (roles is null)
            return true;                       // página anónima
        if (roles.Count == 0)
            return rolActual is not null;      // autenticación genérica
        return rolActual is not null && roles.Contains(rolActual);
    }

    /// <summary>Texto del tooltip para un módulo bloqueado, en español.</summary>
    public string TextoBloqueo(string ruta, string? rolActual)
    {
        var rol = string.IsNullOrWhiteSpace(rolActual) ? "sin sesión" : rolActual;
        if (_rolesPorRuta.TryGetValue(ruta, out var roles) && roles is { Count: > 0 })
            return $"🔒 Requiere el rol: {string.Join(", ", roles)} — tu rol actual: {rol}";
        return $"🔒 Módulo no incluido en tu perfil — tu rol actual: {rol}";
    }
}

using GuarinoApp.Application.Common;
using GuarinoApp.Application.Common.Interfaces;

namespace GuarinoApp.Web.Services;

/// <summary>
/// Resolución del rol vigente del circuito para la UI: el primer rol válido de
/// la fuente única <see cref="RoleNames"/> en el que esté el usuario
/// (mismo criterio que MensajeRechazoRol en Infrastructure).
/// </summary>
public static class RolUsuario
{
    public static string? Resolver(ICurrentUserService usuario)
        => RoleNames.Todos.FirstOrDefault(r => usuario.IsInRole(r));
}

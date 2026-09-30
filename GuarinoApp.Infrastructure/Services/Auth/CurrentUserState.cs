using GuarinoApp.Application.Common.Interfaces;
using System.Security.Claims;

namespace GuarinoApp.Infrastructure.Services.Auth;

/// <summary>
/// Holder con el usuario autenticado del circuito (scoped).
/// Escrito por el proveedor de autenticación; leído por CurrentUserService.
/// </summary>
public sealed class CurrentUserState : ICurrentUserState
{
    public ClaimsPrincipal? User { get; set; }
}

using GuarinoApp.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace GuarinoApp.Infrastructure.Services;

/// <summary>
/// Expone el usuario actual: prioriza la sesión del circuito
/// (ICurrentUserState, escrita por el proveedor de autenticación) y, durante
/// la petición HTTP inicial, el usuario de HttpContext.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ICurrentUserState _state;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor, ICurrentUserState state)
        => (_httpContextAccessor, _state) = (httpContextAccessor, state);

    private ClaimsPrincipal? User => _state.User ?? _httpContextAccessor.HttpContext?.User;

    public string? UserId => User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    public string? UserName => User?.Identity?.Name;
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;
    public IEnumerable<Claim> Claims => User?.Claims ?? Enumerable.Empty<Claim>();

    public bool IsInRole(string role)
        => User?.IsInRole(role) ?? false;
}

using System.Security.Claims;
using GuarinoApp.Application.Common;
using GuarinoApp.Application.Common.Interfaces;
using Microsoft.AspNetCore.Components.Authorization;

namespace GuarinoApp.Web;

/// <summary>
/// Identidad sintética usada exclusivamente por el entorno Testing para que
/// las pruebas de integración puedan ejercitar AuthorizeRouteView en circuitos
/// Blazor. Nunca se registra fuera de ese entorno.
/// </summary>
internal sealed class TestingComponentAuthenticationStateProvider(
    IHttpContextAccessor httpContextAccessor)
    : AuthenticationStateProvider, IAuthenticationStateNotifier
{
    private Task<AuthenticationState>? _state;

    public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
        _state ??= ResolveAsync();

    private Task<AuthenticationState> ResolveAsync()
    {
        var request = httpContextAccessor.HttpContext?.Request;
        var role = request?.Cookies["test_role"]
            ?? request?.Headers["X-Test-Role"].FirstOrDefault();
        ClaimsIdentity identity;
        if (role is not null && RoleNames.Todos.Contains(role, StringComparer.OrdinalIgnoreCase))
        {
            identity = new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, $"test-{role.ToLowerInvariant()}"),
                    new Claim(ClaimTypes.Name, $"demo-{role.ToLowerInvariant()}"),
                    new Claim(ClaimTypes.Role, role)
                }, "Testing");
        }
        else
        {
            identity = new ClaimsIdentity();
        }

        return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
    }

    public void NotifyChanged()
    {
        _state = null;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}

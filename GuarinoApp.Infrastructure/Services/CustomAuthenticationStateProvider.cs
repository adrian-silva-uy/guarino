using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Settings;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace GuarinoApp.Infrastructure.Services;

/// <summary>
/// Proveedor de estado de autenticación personalizado.
/// Prioriza la identidad de Windows (Negotiate) cuando está disponible y,
/// si no lo está ( HttpContext es null durante la vida del circuito de
/// Blazor Server), se identifica al usuario con su usuario de Windows local
/// y el rol configurado en SecuritySettings, de modo que la aplicación siempre
/// muestre un usuario autenticado con su rol correcto.
/// </summary>
public class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
    private static readonly string[] KnownRoles = { "Gerencia", "Contador", "Operador" };

    private readonly ICurrentUserService _currentUserService;
    private readonly SecuritySettings _settings;

    public CustomAuthenticationStateProvider(
        ICurrentUserService currentUser,
        IOptions<SecuritySettings> settings)
    {
        _currentUserService = currentUser;
        _settings = settings.Value;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var identity = BuildIdentity();
        return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
    }

    private ClaimsIdentity BuildIdentity()
    {
        // 1) Identidad real de Windows (Negotiate) cuando está disponible.
        if (_currentUserService.IsAuthenticated)
        {
            var claims = _currentUserService.Claims
                .Where(c => c.Type != ClaimTypes.Role && c.Type != ClaimTypes.NameIdentifier && c.Type != ClaimTypes.Name)
                .ToList();

            claims.Add(new Claim(ClaimTypes.NameIdentifier, _currentUserService.UserId ?? _currentUserService.UserName ?? ""));
            claims.Add(new Claim(ClaimTypes.Name, _currentUserService.UserName ?? "Usuario"));

            foreach (var role in KnownRoles.Where(r => _currentUserService.IsInRole(r)))
                claims.Add(new Claim(ClaimTypes.Role, role));

            return new ClaimsIdentity(claims, "GuarinoAuth");
        }

        // 2) Identidad local de respaldo: usuario de Windows de la máquina.
        if (!_settings.EnableFallbackIdentity)
            return new ClaimsIdentity();

        var userName = Environment.UserName;
        var fallbackRole = ResolveRole(userName);

        return new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userName),
            new Claim(ClaimTypes.Name, userName),
            new Claim(ClaimTypes.Role, fallbackRole)
        }, "GuarinoFallback");
    }

    private string ResolveRole(string userName)
        => _settings.UserRoles.TryGetValue(userName, out var role) && !string.IsNullOrWhiteSpace(role)
            ? role
            : _settings.DefaultRole;

    /// <summary>
    /// Notifica que el estado de autenticación ha cambiado.
    /// Llamar después de login/logout o cuando cambia el usuario.
    /// </summary>
    public void NotifyAuthenticationStateChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}

using GuarinoApp.Application.Common;
using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Settings;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using System.Security.Claims;

namespace GuarinoApp.Infrastructure.Services;

/// <summary>
/// Proveedor de estado de autenticación personalizado.
///
/// Modo Local: valida la sesión desde el token guardado en el navegador
/// (firmado en el servidor) y la mantiene en memoria durante el circuito.
///
/// Modo Windows: usa la identidad de Windows (Negotiate/AD) cuando está
/// disponible y, si no, la identidad de respaldo de SecuritySettings.
///
/// El resultado se cachea en ICurrentUserState para que la auditoría y la
/// autorización siempre vean al mismo usuario dentro del circuito.
/// </summary>
public class CustomAuthenticationStateProvider : AuthenticationStateProvider, IAuthenticationStateNotifier
{
    private static readonly string[] KnownRoles = { "Admin", "Gerencia", "Contador", "Operador" };

    /// <summary>Tarea única en vuelo para resolver la sesión (evita consultas concurrentes a la BD).</summary>
    private Task<AuthenticationState>? _pendingState;

    private readonly ICurrentUserState _state;
    private readonly IAuthTokenService _tokenService;
    private readonly IApplicationDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IJSRuntime _jsRuntime;
    private readonly AuthSettings _authSettings;
    private readonly SecuritySettings _securitySettings;

    public CustomAuthenticationStateProvider(
        ICurrentUserState state,
        IAuthTokenService tokenService,
        IApplicationDbContext dbContext,
        IHttpContextAccessor httpContextAccessor,
        IJSRuntime jsRuntime,
        IOptions<AuthSettings> authSettings,
        IOptions<SecuritySettings> securitySettings)
    {
        _state = state;
        _tokenService = tokenService;
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
        _jsRuntime = jsRuntime;
        _authSettings = authSettings.Value;
        _securitySettings = securitySettings.Value;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        // Sesión ya resuelta en este circuito.
        if (_state.User != null)
            return Task.FromResult(new AuthenticationState(_state.User));

        // Single-flight: al iniciar el circuito, varios componentes (gate del
        // layout, cabecera, autorización de rutas) piden el estado a la vez.
        // Se comparte UNA única tarea para que la consulta a la base de datos
        // se ejecute una sola vez: el DbContext es scoped y no admite
        // operaciones concurrentes sobre la misma instancia.
        _pendingState ??= ResolveAuthenticationStateAsync();
        return _pendingState;
    }

    private async Task<AuthenticationState> ResolveAuthenticationStateAsync()
    {
        if (_authSettings.IsWindowsMode)
        {
            var windowsIdentity = BuildWindowsIdentity();
            if (windowsIdentity != null)
            {
                var principal = new ClaimsPrincipal(windowsIdentity);
                _state.User = principal;
                return new AuthenticationState(principal);
            }

            if (_securitySettings.EnableFallbackIdentity)
            {
                var fallbackPrincipal = new ClaimsPrincipal(BuildFallbackIdentity());
                _state.User = fallbackPrincipal;
                return new AuthenticationState(fallbackPrincipal);
            }
        }
        else
        {
            var localIdentity = await BuildLocalIdentityAsync();
            if (localIdentity != null)
            {
                var principal = new ClaimsPrincipal(localIdentity);
                _state.User = principal;
                return new AuthenticationState(principal);
            }
        }

        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
    }

    /// <summary>
    /// Invalida la sesión en memoria y notifica a los componentes.
    /// Llamar después de iniciar o cerrar sesión.
    /// </summary>
    public void NotifyChanged()
    {
        _state.User = null;
        _pendingState = null;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    /// <summary>Modo Windows: identidad real de Windows si está autenticada.</summary>
    private ClaimsIdentity? BuildWindowsIdentity()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
            return null;

        var name = user.Identity.Name ?? "Usuario";

        // Roles: grupos de Windows que coincidan con los roles de la app
        // (acepta "Gerencia" o "DOMINIO\Gerencia") o asignación por usuario.
        var roles = KnownRoles
            .Where(r => user.IsInRole(r)
                || user.Claims.Any(c => c.Type == ClaimTypes.Role
                    && (c.Value.Equals(r, StringComparison.OrdinalIgnoreCase)
                        || c.Value.EndsWith("\\" + r, StringComparison.OrdinalIgnoreCase))))
            .ToList();

        if (roles.Count == 0
            && _securitySettings.UserRoles.TryGetValue(name, out var mappedRole)
            && !string.IsNullOrWhiteSpace(mappedRole))
        {
            roles.Add(mappedRole);
        }

        if (roles.Count == 0)
            roles.Add(_securitySettings.DefaultRole);

        var claims = user.Claims
            .Where(c => c.Type != ClaimTypes.Role && c.Type != ClaimTypes.NameIdentifier && c.Type != ClaimTypes.Name)
            .ToList();

        claims.Add(new Claim(ClaimTypes.NameIdentifier, user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? name));
        claims.Add(new Claim(ClaimTypes.Name, name));

        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        return new ClaimsIdentity(claims, "GuarinoAuth");
    }

    /// <summary>Modo Windows: identidad de respaldo (usuario de Windows local).</summary>
    private ClaimsIdentity BuildFallbackIdentity()
    {
        var userName = Environment.UserName;
        var role = _securitySettings.UserRoles.TryGetValue(userName, out var configuredRole)
                   && !string.IsNullOrWhiteSpace(configuredRole)
            ? configuredRole
            : _securitySettings.DefaultRole;

        return new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userName),
            new Claim(ClaimTypes.Name, userName),
            new Claim(ClaimTypes.Role, role)
        }, "GuarinoFallback");
    }

    /// <summary>Modo Local: sesión a partir del token guardado en el navegador.</summary>
    private async Task<ClaimsIdentity?> BuildLocalIdentityAsync()
    {
        try
        {
            var token = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", AuthStorage.TokenKey);
            if (string.IsNullOrWhiteSpace(token))
                return null;

            var payload = _tokenService.Validate(token);
            if (payload == null)
                return null;

            // Revalidación contra la base de datos al iniciar el circuito:
            // - si el usuario fue desactivado o eliminado, la sesión se corta;
            // - el rol vigente se toma de la BD (los cambios de rol y la
            //   desactivación surten efecto en el próximo inicio de sesión
            //   de la página, sin esperar a que venza el token).
            var user = await _dbContext.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserName == payload.UserName && !u.IsDeleted);

            if (user == null || !user.IsActive)
                return null;

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.UserName),
                new(ClaimTypes.Role, user.Role.Name)
            };

            if (!string.IsNullOrWhiteSpace(user.FullName))
                claims.Add(new Claim(AuthClaims.FullName, user.FullName));

            return new ClaimsIdentity(claims, "GuarinoAuth");
        }
        catch
        {
            // Interop JS no disponible (p. ej. antes de que el circuito esté listo).
            return null;
        }
    }
}

using GuarinoApp.Application.Common.Interfaces;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace GuarinoApp.Infrastructure.Services;

/// <summary>
/// Proveedor de estado de autenticación personalizado que usa ICurrentUserService
/// para obtener la información del usuario actual. Funciona correctamente con
/// Blazor Server y autenticación Negotiate (Windows Auth).
/// </summary>
public class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly ICurrentUserService _currentUserService;

    public CustomAuthenticationStateProvider(ICurrentUserService currentUser)
    {
        _currentUserService = currentUser;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var identity = new ClaimsIdentity();

        if (_currentUserService.IsAuthenticated)
        {
            identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, _currentUserService.UserId ?? ""),
                new Claim(ClaimTypes.Name, _currentUserService.UserName ?? "Usuario"),
                new Claim(ClaimTypes.Role, 
                    _currentUserService.IsInRole("Gerencia") ? "Gerencia" :
                    _currentUserService.IsInRole("Contador") ? "Contador" :
                    _currentUserService.IsInRole("Operador") ? "Operador" : "Usuario")
            }, "GuarinoAuth");
        }

        var user = new ClaimsPrincipal(identity);
        return Task.FromResult(new AuthenticationState(user));
    }

    /// <summary>
    /// Notifica que el estado de autenticación ha cambiado.
    /// Llamar después de login/logout o cuando cambia el usuario.
    /// </summary>
    public void NotifyAuthenticationStateChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}

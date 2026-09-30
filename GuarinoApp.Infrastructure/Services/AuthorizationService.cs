using GuarinoApp.Application.Common.Interfaces;

namespace GuarinoApp.Infrastructure.Services;

/// <summary>
/// Implementación del servicio de autorización basado en roles.
/// </summary>
public class AuthorizationService : IAuthorizationService
{
    private readonly ICurrentUserService _currentUser;

    public AuthorizationService(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    public Task<bool> IsInRoleAsync(string role)
    {
        return Task.FromResult(_currentUser.IsInRole(role));
    }

    public Task<bool> IsInAnyRoleAsync(params string[] roles)
    {
        return Task.FromResult(roles.Any(r => _currentUser.IsInRole(r)));
    }

    public Task<bool> IsAuthenticatedAsync()
    {
        return Task.FromResult(_currentUser.IsAuthenticated);
    }

    public Task<string?> GetUserNameAsync()
    {
        return Task.FromResult(_currentUser.UserName);
    }
}

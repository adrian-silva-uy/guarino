namespace GuarinoApp.Application.Common.Interfaces;

/// <summary>
/// Servicio para verificar autorización basada en roles.
/// </summary>
public interface IAuthorizationService
{
    /// <summary>
    /// Verifica si el usuario actual tiene el rol especificado.
    /// </summary>
    Task<bool> IsInRoleAsync(string role);

    /// <summary>
    /// Verifica si el usuario actual tiene alguno de los roles especificados.
    /// </summary>
    Task<bool> IsInAnyRoleAsync(params string[] roles);

    /// <summary>
    /// Verifica si el usuario actual está autenticado.
    /// </summary>
    Task<bool> IsAuthenticatedAsync();

    /// <summary>
    /// Obtiene el nombre del usuario actual.
    /// </summary>
    Task<string?> GetUserNameAsync();
}

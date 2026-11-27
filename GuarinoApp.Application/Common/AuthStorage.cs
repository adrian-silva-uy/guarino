namespace GuarinoApp.Application.Common;

/// <summary>
/// Nombre de la cookie HttpOnly usada por la sesión.
/// </summary>
public static class AuthStorage
{
    /// <summary>Cookie que transporta el token protegido de sesión.</summary>
    public const string CookieName = "auth_token";
}

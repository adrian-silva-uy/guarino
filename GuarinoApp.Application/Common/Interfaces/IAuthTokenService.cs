namespace GuarinoApp.Application.Common.Interfaces;

/// <summary>
/// Emite y valida los tokens de sesión de la aplicación.
/// El token es un valor firmado en el servidor: no se puede alterar desde el
/// navegador sin perder la firma.
/// </summary>
public interface IAuthTokenService
{
    /// <summary>Emite un token firmado con los datos de la sesión.</summary>
    string Issue(AuthTokenPayload payload);

    /// <summary>Valida el token y devuelve sus datos, o null si es inválido o venció.</summary>
    AuthTokenPayload? Validate(string token);
}

/// <summary>Datos contenidos en el token de sesión.</summary>
public class AuthTokenPayload
{
    /// <summary>Nombre de usuario.</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>Nombre completo a mostrar.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Rol de la aplicación.</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>Fecha y hora (UTC) de vencimiento de la sesión.</summary>
    public DateTime ExpiresAtUtc { get; set; }
}

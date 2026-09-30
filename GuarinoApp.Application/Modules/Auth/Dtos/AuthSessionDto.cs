namespace GuarinoApp.Application.Modules.Auth.Dtos;

/// <summary>Resultado de un inicio de sesión o registro.</summary>
public class AuthSessionDto
{
    /// <summary>True cuando la operación salió bien.</summary>
    public bool Success { get; set; }

    /// <summary>Mensaje de error apto para mostrar al usuario (si falló).</summary>
    public string? Error { get; set; }

    /// <summary>Nombre de usuario de la sesión.</summary>
    public string? UserName { get; set; }

    /// <summary>Nombre completo de la sesión.</summary>
    public string? FullName { get; set; }

    /// <summary>Rol de la sesión.</summary>
    public string? Role { get; set; }

    /// <summary>Token de sesión a guardar en el navegador.</summary>
    public string? Token { get; set; }

    /// <summary>Falla con un mensaje apto para el usuario.</summary>
    public static AuthSessionDto Fail(string error) => new() { Success = false, Error = error };
}

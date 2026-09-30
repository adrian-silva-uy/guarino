using GuarinoApp.Domain.Shared;

namespace GuarinoApp.Domain.Entities;

/// <summary>
/// Usuario de la aplicación (autenticación local con usuario y contraseña).
/// En modo Windows/AD no se usa esta tabla para el ingreso, pero puede usarse
/// para usuarios de respaldo.
/// </summary>
public class AppUser : BaseEntity
{
    /// <summary>Nombre de usuario para ingresar (único).</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>Nombre completo a mostrar en la interfaz.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Contraseña en formato PBKDF2 (nunca en texto plano).</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Rol de la aplicación: Gerencia, Contador u Operador.</summary>
    public string Role { get; set; } = "Operador";

    /// <summary>Indica si el usuario puede ingresar.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Fecha y hora del último inicio de sesión exitoso.</summary>
    public DateTime? LastLoginAt { get; set; }
}

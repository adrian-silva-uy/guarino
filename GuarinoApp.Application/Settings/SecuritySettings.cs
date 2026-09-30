namespace GuarinoApp.Application.Settings;

/// <summary>
/// Configuración de identidad y roles de la aplicación.
/// </summary>
public class SecuritySettings
{
    public const string SectionName = "Security";

    /// <summary>
    /// Cuando la autenticación de Windows no entrega identidad (p. ej. fuera del
    /// dominio o en local), se identifica al usuario con su usuario de Windows
    /// local y el rol que le corresponda según <see cref="UserRoles"/>.
    /// </summary>
    public bool EnableFallbackIdentity { get; set; } = true;

    /// <summary>Rol asignado cuando el usuario no tiene un rol configurado.</summary>
    public string DefaultRole { get; set; } = "Gerencia";

    /// <summary>
    /// Asignación explícita de rol por usuario de Windows.
    /// La clave se compara sin distinguir mayúsculas/minúsculas.
    /// </summary>
    public Dictionary<string, string> UserRoles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

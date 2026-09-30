namespace GuarinoApp.Application.Settings;

/// <summary>
/// Configuración del modo de autenticación de la aplicación.
/// </summary>
public class AuthSettings
{
    public const string SectionName = "Auth";

    /// <summary>
    /// "Local": ingreso con usuario y contraseña almacenados en la base de datos.
    /// "Windows": ingreso con la identidad de Windows (Active Directory) del servidor.
    /// </summary>
    public string Mode { get; set; } = "Local";

    /// <summary>Permite que los usuarios se registren desde la pantalla de acceso.</summary>
    public bool AllowRegistration { get; set; } = true;

    /// <summary>Vigencia de la sesión (token) expresada en horas.</summary>
    public int TokenValidityHours { get; set; } = 12;

    /// <summary>Rol que se asigna a los usuarios nuevos (el primero siempre es Gerencia).</summary>
    public string DefaultRole { get; set; } = "Operador";

    /// <summary>True cuando la autenticación se hace con la identidad de Windows.</summary>
    public bool IsWindowsMode =>
        string.Equals(Mode, "Windows", StringComparison.OrdinalIgnoreCase);
}

namespace GuarinoApp.Infrastructure.Services;

/// <summary>
/// Opciones de configuración SMTP, leídas desde la sección "Email" de appsettings.json
/// </summary>
public class EmailOptions
    {
    public const string SectionName = "Email";

    public string SmtpServer { get; set; } = "";
    public int SmtpPort { get; set; } = 587;
    public string FromEmail { get; set; } = ""; // buzón de empresa (dirección autenticada por defecto)
    public string FromName { get; set; } = "";  // nombre visible de la empresa
    public string? SmtpUser { get; set; } = null; // usuario SMTP (si null usa FromEmail)
    public string Password { get; set; } = "";
    public bool EnableSsl { get; set; } = true;
    public bool UseDefaultCredentials { get; set; } = false;

    // Envelope-from (MAIL FROM) que debe coincidir con el dominio autorizado
    public string? EnvelopeFrom { get; set; } = null;

    // Comportamiento para producción:
    // Si true, siempre se enviará autenticado desde el buzón de la empresa (FromEmail/SmtpUser),
    // pero el nombre visible podrá incluir el usuario logueado y Reply-To apuntará al usuario.
    public bool UseCompanyMailboxForSending { get; set; } = true;

    // Si true, el displayName del From será "<Usuario> via <Empresa>" cuando UseCompanyMailboxForSending = true.
    public bool UseUserNameInFromDisplay { get; set; } = true;

    // Si true, añade Reply-To con el email del usuario logueado (si se proporciona).
    public bool UseReplyToUser { get; set; } = true;

    // OAuth2 support (opcional)
    public bool UseOAuth2 { get; set; } = false;
    public string? OAuth2AccessToken { get; set; } = null;
    }

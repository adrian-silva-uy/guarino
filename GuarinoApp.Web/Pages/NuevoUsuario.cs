namespace GuarinoApp.Web.Pages;

/// <summary>Datos del formulario de alta de usuarios (la contraseña solo vive en
/// memoria hasta hashearse en el servidor).</summary>
internal sealed class NuevoUsuario
    {
    public string UserName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = "Operador";
    }

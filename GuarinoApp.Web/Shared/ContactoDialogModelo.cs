using GuarinoApp.Domain.Entities;

namespace GuarinoApp.Web.Shared;

/// <summary>Datos del formulario de ContactoDialog que la página recibe al cerrar el diálogo.</summary>
public class ContactoDialogModelo
    {
    public string Nombre { get; set; } = string.Empty;
    public string Empresa { get; set; } = string.Empty;
    public string? Pais { get; set; }
    public string? Email { get; set; }
    public string? Telefono { get; set; }
    public string? Interes { get; set; }
    public string? Notas { get; set; }
    public string Estado { get; set; } = ContactoEstados.Nuevo;
    }

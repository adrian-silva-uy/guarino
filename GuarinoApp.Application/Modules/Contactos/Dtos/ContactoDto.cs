namespace GuarinoApp.Application.Modules.Contactos.Dtos;

/// <summary>Vista de un contacto para la pantalla y el CSV.</summary>
public class ContactoDto
    {
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Empresa { get; set; } = string.Empty;
    public string? Pais { get; set; }
    public string? Email { get; set; }
    public string? Telefono { get; set; }
    public string? Interes { get; set; }
    public string? Notas { get; set; }
    public string Estado { get; set; } = string.Empty;
    public DateTime FechaAlta { get; set; }
    public string? CreadoPor { get; set; }
    public DateTime? FechaModificacion { get; set; }
    public string? Origen { get; set; }
    }

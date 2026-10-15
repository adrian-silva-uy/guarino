using GuarinoApp.Domain.Shared;

namespace GuarinoApp.Domain.Entities;

/// <summary>Estados posibles de un contacto comercial (lead).</summary>
public static class ContactoEstados
    {
    public const string Nuevo = "Nuevo";
    public const string EnSeguimiento = "En seguimiento";
    public const string Cerrado = "Cerrado";
    public const string Descartado = "Descartado";
    public static readonly string[] Todos = { Nuevo, EnSeguimiento, Cerrado, Descartado };
    }

/// <summary>
/// Contacto comercial: lead capturado en ferias, reuniones o consultas
/// (compradores, distribuidores, proveedores). No es un cliente del sistema,
/// es un registro de seguimiento comercial.
/// </summary>
public class Contacto : BaseEntity
    {
    public string Nombre { get; set; } = string.Empty;
    public string Empresa { get; set; } = string.Empty;
    public string? Pais { get; set; }
    public string? Email { get; set; }
    public string? Telefono { get; set; }

    /// <summary>Qué le interesó (variedades, volúmenes, mercado, etc.).</summary>
    public string? Interes { get; set; }

    public string? Notas { get; set; }
    public string Estado { get; set; } = ContactoEstados.Nuevo;
    }

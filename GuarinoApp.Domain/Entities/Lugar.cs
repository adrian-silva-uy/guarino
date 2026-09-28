namespace GuarinoApp.Domain.Entities;

/// <summary>
/// Tabla "Lugares" (antes consultada vía SQL crudo en GetLugaresQueryHandler). No hereda
/// BaseEntity a propósito: no tengo confirmación de que la tabla real tenga las columnas de
/// auditoría que BaseEntity agregaría -- si las tiene, avisame y la hago heredar como el resto
/// de las entidades del dominio.
/// </summary>
public class Lugar
    {
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Tipo { get; set; }
    public string? Direccion { get; set; }
    }

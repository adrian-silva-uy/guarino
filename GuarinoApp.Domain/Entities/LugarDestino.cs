namespace GuarinoApp.Domain.Entities;

/// <summary>
/// Tabla "LugarDestino" (antes consultada vía SQL crudo en GetLugaresDestinoQueryHandler). Mismo
/// criterio que Lugar/LugarOrigen: sin heredar BaseEntity, clave primaria con el nombre real de
/// columna (IdLugarDestino) tal como la usaba el SQL original.
/// </summary>
public class LugarDestino
    {
    public int IdLugarDestino { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Direccion { get; set; }
    public string? Localidad { get; set; }
    }

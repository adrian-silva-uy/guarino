namespace GuarinoApp.Domain.Entities;

/// <summary>
/// Tabla "LugarOrigen" (antes consultada vía SQL crudo en GetLugaresOrigenQueryHandler). Mismo
/// criterio que Lugar: sin heredar BaseEntity, clave primaria con el nombre real de columna
/// (IdLugarOrigen) tal como la usaba el SQL original.
/// </summary>
public class LugarOrigen
    {
    public int IdLugarOrigen { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Direccion { get; set; }
    public string? Localidad { get; set; }
    }

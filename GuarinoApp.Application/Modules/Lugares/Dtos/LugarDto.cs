namespace GuarinoApp.Application.Modules.Lugares.Dtos;
public class LugarDto
    {
    public int Id { get; set; }
    public string? Nombre { get; set; }
    public string? Tipo { get; set; } // "Origen" o "Destino"
    public string? Direccion { get; set; }
    }

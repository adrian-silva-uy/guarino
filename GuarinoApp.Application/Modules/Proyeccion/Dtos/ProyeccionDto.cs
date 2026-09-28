namespace GuarinoApp.Application.Modules.Proyeccion.Dtos;
public class ProyeccionDto
    {
    public string? Periodo { get; set; }
    public int Mes { get; set; }
    public int Año { get; set; }
    public decimal KgsVolcados { get; set; }
    public decimal KgsProyectados { get; set; }
    public decimal Tendencia { get; set; }
    public bool EsProyectado { get; internal set; }
    }

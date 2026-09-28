namespace GuarinoApp.Application.Modules.ResumenMensual.Dtos;
public class ResumenMensualDto
    {
    public int Año { get; set; }
    public int Mes { get; set; }
    public string? Nombre { get; set; }
    public decimal Kgs_Volcados { get; set; }
    public int BinsVolcados { get; set; }
    public string? Balanza { get; set; }
    }

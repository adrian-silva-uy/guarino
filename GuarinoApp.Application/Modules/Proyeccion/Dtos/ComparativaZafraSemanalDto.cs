namespace GuarinoApp.Application.Modules.Proyeccion.Dtos;

public class ComparativaZafraSemanalDto
{
    public int Semana { get; set; }
    public int Zafra { get; set; }
    public decimal KgVolcadosAcumulados { get; set; }
    public decimal PalletsDespachadosAcumulados { get; set; }
    public decimal? FacturacionAcumuladaUsd { get; set; }
}

public class ComparativaZafraSemanalResultDto
{
    public List<ComparativaZafraSemanalDto> Datos { get; set; } = new();
    public List<int> Zafras { get; set; } = new();
    public List<int> Semanas { get; set; } = new();
}

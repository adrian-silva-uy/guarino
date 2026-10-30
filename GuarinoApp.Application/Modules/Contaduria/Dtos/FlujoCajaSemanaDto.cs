namespace GuarinoApp.Application.Modules.Contaduria.Dtos;

public class FlujoCajaSemanaDto
{
    public int Semana { get; set; }              // 1 a 13 (relativa a hoy)
    public DateTime FechaDesde { get; set; }
    public DateTime FechaHasta { get; set; }
    public string Escenario { get; set; } = "Base"; // Base, Pesimista, Optimista
    public decimal EntradasUsd { get; set; }     // cobros por embarque
    public decimal SalidasLiquidacionesQuintaUsd { get; set; }
    public decimal SalidasJornalesUsd { get; set; }
    public decimal SalidasFletesUsd { get; set; }
    public decimal SalidasImpuestosUsd { get; set; }
    public decimal SalidasOtrosUsd { get; set; }
    public decimal SaldoAcumuladoUsd { get; set; }
    public bool EsReal { get; set; }
}

public class ProyeccionCajaResultDto
{
    public List<FlujoCajaSemanaDto> Datos { get; set; } = new();
    public decimal SaldoInicialUsd { get; set; }
    public decimal SaldoMinimoUsd => Datos.Any() ? Datos.Min(d => d.SaldoAcumuladoUsd) : 0;
    public int SemanaPiso => Datos.Any() ? Datos.IndexOf(Datos.OrderBy(d => d.SaldoAcumuladoUsd).First()) + 1 : 0;
    public string Escenario { get; set; } = "Base";
}

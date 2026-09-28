namespace GuarinoApp.Application.Modules.Proyeccion.Dtos;

public class ZafraDto
    {
    public int Año { get; set; }
    public decimal TotalKgs { get; set; }
    public decimal VariacionPorcentual { get; set; } // respecto a la zafra anterior
    public string? MesesPico { get; set; } // ej. "Marzo, Junio, Septiembre"
    public int NumeroMeses { get; set; }
    public string? Estado { get; set; } // "Histórico", "Proyectado", "Actual"
    public string? Comentario { get; set; }

    // Para que la UI sepa si la fila viene de datos reales o de la simulación de respaldo.
    public bool EsDummy { get; set; }

    // Agregado: "Impacto Operativo" 
    // (25kg/bin, 25tn/camión, 10tn/hora). Lo calcula el handler y llega ya resuelto.
    // Solo se completan en la fila "Proyectado"; en el resto quedan en null/false.
    public decimal? BinsEstimados { get; set; }
    public decimal? CamionesEstimados { get; set; }
    public decimal? HorasEmpaqueEstimadas { get; set; }
    public bool RiesgoCapacidad { get; set; }
    public decimal? CapacidadMaximaHistoricaKgs { get; set; }

    // Explica de dónde salió cada supuesto usado (kg/bin real vs. parámetro, etc.) -- para que
    // Gerencia/Supervisores vean la base del cálculo, no solo el resultado.
    public string? SupuestosOperativos { get; set; }
    }

using GuarinoApp.Domain.Shared;

namespace GuarinoApp.Domain.Entities;

/// <summary>
/// Registro de eventos climáticos detectados automáticamente o cargados manualmente
/// para gestión de riesgo en producción citrícola.
/// Optimizado para región: Salto, Paysandú (Uruguay) y Norte de Brasil.
/// </summary>
public class EventoClimatico : BaseEntity
    {
    /// <summary>
    /// Lote donde ocurrió el evento (obligatorio para granularidad)
    /// </summary>
    public int LoteId { get; set; }

    /// <summary>
    /// Productor asociado al lote (FK derivada de Lote)
    /// </summary>
    public int ProductorId { get; set; }

    /// <summary>
    /// Fecha del evento (fecha del día, no hora exacta)
    /// </summary>
    public DateTime Fecha { get; set; }

    /// <summary>
    /// Tipo de evento climático
    /// Valores: Helada, Granizo, LluviaExcesiva, VientoFuerte, CalorExtremo
    /// </summary>
    public string TipoEvento { get; set; } = string.Empty;

    /// <summary>
    /// Severidad estimada del evento
    /// Valores: Baja, Media, Alta, Crítica
    /// </summary>
    public string SeveridadEstimada { get; set; } = string.Empty;

    /// <summary>
    /// Porcentaje de pérdida estimada en la producción (0-100)
    /// Calculado automáticamente por el algoritmo o ajustado manualmente
    /// </summary>
    public decimal? PorcentajePerdidaEstimada { get; set; }

    /// <summary>
    /// Kilos de producción estimados perdidos.
    /// Calculado al persistir el evento: PorcentajePerdidaEstimada × KilosNetos
    /// del lote (snapshot: el valor histórico no cambia si el lote se actualiza
    /// después). Lo completa OpenMeteoClimateService.CalcularKilosPerdidosAsync.
    /// </summary>
    public decimal? KilosPerdidosEstimados { get; set; }

    /// <summary>
    /// Fuente del dato: "Detección Automática - OpenMeteo" o "Reporte de Campo - [Nombre]"
    /// </summary>
    public string Fuente { get; set; } = string.Empty;

    /// <summary>
    /// Observaciones adicionales (ej: temperatura mínima, acumulado de lluvia)
    /// </summary>
    public string? Observaciones { get; set; }

    // Navegaciones
    public virtual Lote Lote { get; set; } = null!;
    public virtual Productor Productor { get; set; } = null!;
    }

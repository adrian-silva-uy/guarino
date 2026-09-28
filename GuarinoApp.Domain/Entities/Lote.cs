using GuarinoApp.Domain.Shared;
namespace GuarinoApp.Domain.Entities;
public class Lote : BaseEntity, IAggregateRoot
    {
    public string Codigo { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public int VariedadId { get; set; }
    public int QuintaId { get; set; }
    public string? Zafra { get; set; }
    public int? TipoLoteId { get; set; }

    // columnas que usaba el SQL
    public int CantidadBins { get; set; }
    public int CantidadBinsDesp { get; set; }
    public int CantidadBinsEmb { get; set; }
    public decimal KilosNetos { get; set; }
    public DateTime? FechaIngreso { get; set; }
    public string? IdRemito { get; set; }

    public int ProductorId { get; set; }
    public int? BasculaId { get; set; }
    public int? DestinoInternoId { get; set; }
    public int EmpaqueId { get; set; }

    // navegaciones
    public virtual Variedad Variedad { get; set; } = null!;
    public virtual Quinta Quinta { get; set; } = null!;
    public virtual TipoLote? TipoLote { get; set; }
    public virtual Productor Productor { get; set; } = null!;
    public virtual Bascula? Bascula { get; set; }
    public virtual DestinoInterno? DestinoInterno { get; set; }
    public virtual Empaque Empaque { get; set; } = null!;
    }

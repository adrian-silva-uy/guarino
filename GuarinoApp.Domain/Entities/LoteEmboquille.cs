using GuarinoApp.Domain.Shared;
namespace GuarinoApp.Domain.Entities;
public class LoteEmboquille : BaseEntity
    {
    // FKs explícitas a Lote y Emboquille: antes existían AMBOS pares
    // (IdLote/IdEmboquille como columnas muertas + LoteId/EmboquilleId como
    // shadow FK de EF), una violación de normalización con 4 columnas para
    // 2 relaciones. Hoy LoteId/EmboquilleId son la única fuente de verdad.
    public int LoteId { get; set; }
    public int EmboquilleId { get; set; }
    public DateTime FechaProduccion { get; set; }
    public decimal KilosNetos { get; set; }
    public int CantidadBins { get; set; }
    public string? Zafra { get; set; } // Columna para evitar clonación de bases

    // campos necesarios para reconstruir "Stock en Playa" (antes vivían en el
    // Lote legacy como columnas planas; acá se modelan por evento de pesaje/emboquille).
    public int? IdBascula { get; set; }
    public int? IdDestinoInterno { get; set; }
    public int? IdEmpaque { get; set; }
    public int CantidadBinsDespachados { get; set; }
    public int CantidadBinsVolcados { get; set; }
    public string Estado { get; set; } = "E"; // 'E' = en playa / activo

    public virtual Lote Lote { get; set; } = null!;
    public virtual Emboquille Emboquille { get; set; } = null!;
    public virtual Bascula? Bascula { get; set; }
    public virtual DestinoInterno? DestinoInterno { get; set; }
    public virtual Empaque? Empaque { get; set; }
    }

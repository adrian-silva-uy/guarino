using GuarinoApp.Domain.Enums;
using GuarinoApp.Domain.Shared;

namespace GuarinoApp.Domain.Entities;

public class Pallet : BaseEntity, IAggregateRoot
    {
    public string Codigo { get; set; } = string.Empty;
    public int? PalletNumero { get; set; }

    // Volcado opcional (permitir nulls para pallets definitivos)
    public int? VolcadoId { get; set; }

    public string? Orden { get; set; }
    public string? Contramarca { get; set; }
    public string? Calibre { get; set; }
    public string? Envase { get; set; }
    public string? Marca { get; set; }
    public string? Cliente { get; set; }
    public string? Variedad { get; set; }
    public string? Categoria { get; set; }
    public string? EstadoTexto { get; set; } // Representa 'D', 'F', 'P', 'R'
    public EstadoPallet Estado { get; set; }

    public string? Productor { get; set; }
    public string? TipoPallet { get; set; }
    public string? FormaEmbalado { get; set; }
    public string? Frio { get; set; }
    public DateTime? Fecha { get; set; }
    public string? Hora { get; set; }
    public int? SemanaProd { get; set; }
    public string? Embalaje { get; set; }
    public string? Especie { get; set; }
    public string? Produccion { get; set; }
    public string? O_Obs3 { get; set; }
    public string? SPA { get; set; }
    public string? Lote { get; set; }
    public string? Cuadros { get; set; }
    public string? FecCosecha { get; set; }

    public int Cantidad { get; set; } = 1;
    public int? CantidadEnvases { get; set; }
    public decimal? PesoNeto { get; set; }

    // Propiedad de navegación opcional
    public virtual Volcado? Volcado { get; set; }
    }

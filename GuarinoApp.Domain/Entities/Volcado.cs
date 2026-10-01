using GuarinoApp.Domain.Enums;
using GuarinoApp.Domain.Shared;
namespace GuarinoApp.Domain.Entities;
public class Volcado : BaseEntity
    {
    public int LoteId { get; set; }
    public DateTime FechaVolcado { get; set; }
    public int CantidadBins { get; set; }
    public decimal PesoKilos { get; set; }
    // (Eliminado: IdBascula legacy sin FK ni uso — la bascula se referencia por Canal.)
    public CanalDistribucion Canal { get; set; }
    public virtual Lote Lote { get; set; } = null!;
    }

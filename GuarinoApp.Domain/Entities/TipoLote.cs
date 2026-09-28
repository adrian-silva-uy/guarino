using GuarinoApp.Domain.Shared;
namespace GuarinoApp.Domain.Entities;
public class TipoLote : BaseEntity
    {
    public int IdTipoLote { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    }

using GuarinoApp.Domain.Shared;
namespace GuarinoApp.Domain.Entities;
public class Emboquille : BaseEntity
    {
    public int IdEmboquille { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    }

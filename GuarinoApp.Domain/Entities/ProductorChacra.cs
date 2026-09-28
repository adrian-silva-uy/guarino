using GuarinoApp.Domain.Shared;
namespace GuarinoApp.Domain.Entities;
public class ProductorChacra : BaseEntity
    {
    public int IdProductor { get; set; }
    public int IdChacra { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public virtual Productor Productor { get; set; } = null!;
    }

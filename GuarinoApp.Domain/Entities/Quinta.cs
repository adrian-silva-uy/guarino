using GuarinoApp.Domain.Shared;
namespace GuarinoApp.Domain.Entities;
public class Quinta : BaseEntity
    {
    public string Nombre { get; set; } = string.Empty;
    public int ProductorId { get; set; }
    public virtual Productor Productor { get; set; } = null!;
    }

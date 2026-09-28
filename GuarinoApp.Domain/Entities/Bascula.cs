using GuarinoApp.Domain.Shared;
namespace GuarinoApp.Domain.Entities;
public class Bascula : BaseEntity
    {
    public int IdBascula { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    }

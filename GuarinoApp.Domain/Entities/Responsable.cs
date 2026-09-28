using GuarinoApp.Domain.Shared;
namespace GuarinoApp.Domain.Entities;
public class Responsable : BaseEntity
    {
    public int IdResponsable { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    }

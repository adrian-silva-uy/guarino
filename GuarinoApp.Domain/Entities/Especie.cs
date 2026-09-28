using GuarinoApp.Domain.Shared;
namespace GuarinoApp.Domain.Entities;
public class Especie : BaseEntity
    {
    public int IdEspecie { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    }

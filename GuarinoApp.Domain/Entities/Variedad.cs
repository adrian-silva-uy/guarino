using GuarinoApp.Domain.Shared;
namespace GuarinoApp.Domain.Entities;
public class Variedad : BaseEntity
    {
    public string Nombre { get; set; } = string.Empty;
    public Especie? Especie { get; set; }
    public int? EspecieId { get; set; }

    }

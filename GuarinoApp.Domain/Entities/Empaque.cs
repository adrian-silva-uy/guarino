using GuarinoApp.Domain.Shared;
namespace GuarinoApp.Domain.Entities;
public class Empaque : BaseEntity
    {
    public int IdEmpaque { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    }

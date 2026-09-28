using GuarinoApp.Domain.Shared;
namespace GuarinoApp.Domain.Entities;
public class DestinoInterno : BaseEntity
    {
    public int IdDestinoInterno { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    }

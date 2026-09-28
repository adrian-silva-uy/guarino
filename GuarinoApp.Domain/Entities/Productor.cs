using GuarinoApp.Domain.Shared;
namespace GuarinoApp.Domain.Entities;
public class Productor : BaseEntity
    {
    public string Nombre { get; set; } = string.Empty;
    public string RazonSocial { get; set; } = string.Empty;
    public string? Ruc { get; set; }
    }

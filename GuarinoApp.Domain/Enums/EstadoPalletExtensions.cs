namespace GuarinoApp.Domain.Enums;

public static class EstadoPalletExtensions
    {
    public static string ToDisplayName(this EstadoPallet estado) => estado switch
        {
            EstadoPallet.Despachado => "Despachado",
            EstadoPallet.EnFrio => "En Frío",
            EstadoPallet.PreFrio => "Pre Frío",
            EstadoPallet.Rearmado => "Rearmado",
            _ => estado.ToString()
            };

    public static string MapEstado(string? estadoTexto, EstadoPallet estadoEnum) => estadoTexto switch
        {
            "D" => "Despachado",
            "F" => "En Frío",
            "P" => "Pre Frío",
            "R" => "Rearmado",
            _ => estadoEnum.ToDisplayName()
            };
    }

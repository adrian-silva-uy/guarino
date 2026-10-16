namespace GuarinoApp.Application.Modules.Contactos.Dtos;

/// <summary>Resultado de una importación masiva de contactos (patrón de ContactoResultDto).</summary>
public class ImportarContactosResultDto
    {
    public bool Success { get; set; }
    public string? Error { get; set; }

    /// <summary>Contactos realmente dados de alta.</summary>
    public int Creados { get; set; }

    /// <summary>Filas omitidas porque el contacto ya existía.</summary>
    public int Duplicados { get; set; }

    /// <summary>Filas descartadas por no pasar la validación.</summary>
    public int Invalidos { get; set; }

    /// <summary>Motivos (acotados) de duplicados/inválidos, para el resumen.</summary>
    public List<string> Detalle { get; set; } = new();

    public static ImportarContactosResultDto Ok() => new() { Success = true };
    public static ImportarContactosResultDto Fail(string error) => new() { Success = false, Error = error };
    }

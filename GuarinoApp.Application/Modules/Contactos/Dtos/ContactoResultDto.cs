namespace GuarinoApp.Application.Modules.Contactos.Dtos;

/// <summary>Resultado de alta/edición/baja de contactos (patrón de AuthResultDto).</summary>
public class ContactoResultDto
    {
    public bool Success { get; set; }
    public string? Error { get; set; }

    public static ContactoResultDto Ok() => new() { Success = true };
    public static ContactoResultDto Fail(string error) => new() { Success = false, Error = error };
    }

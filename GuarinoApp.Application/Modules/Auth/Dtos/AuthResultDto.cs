namespace GuarinoApp.Application.Modules.Auth.Dtos;

/// <summary>Resultado genérico de una operación sobre usuarios.</summary>
public class AuthResultDto
{
    /// <summary>True cuando la operación salió bien.</summary>
    public bool Success { get; set; }

    /// <summary>Mensaje de error apto para mostrar al usuario (si falló).</summary>
    public string? Error { get; set; }

    /// <summary>Falla con un mensaje apto para el usuario.</summary>
    public static AuthResultDto Fail(string error) => new() { Success = false, Error = error };

    /// <summary>Operación exitosa.</summary>
    public static AuthResultDto Ok() => new() { Success = true };
}

namespace GuarinoApp.Application.Modules.Contactos.Dtos;

/// <summary>
/// Una fila detectada por el importador de contactos (CSV o texto pegado de
/// WhatsApp/correo), todavía sin validar ni deduplicar: eso lo hace el handler.
/// </summary>
public sealed record ContactoImportarDto(
    string Nombre,
    string Empresa,
    string? Pais,
    string? Email,
    string? Telefono,
    string? Interes,
    string? Notas);

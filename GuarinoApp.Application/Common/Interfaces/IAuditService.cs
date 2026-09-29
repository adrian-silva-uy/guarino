namespace GuarinoApp.Application.Common.Interfaces;

/// <summary>
/// Servicio para registrar acciones de usuarios en el log de auditoría.
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Registra una acción en el log de auditoría.
    /// </summary>
    Task LogAsync(string action, string module, string description, string? additionalData = null);
}

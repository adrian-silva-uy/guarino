using GuarinoApp.Domain.Shared;

namespace GuarinoApp.Domain.Entities;

/// <summary>
/// Registro de auditoría para rastrear acciones de usuarios.
/// </summary>
public class LogEntry : BaseEntity
{
    /// <summary>
    /// Usuario que realizó la acción
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Rol del usuario al momento de la acción
    /// </summary>
    public string? Role { get; set; }

    /// <summary>
    /// Tipo de acción (Create, Update, Delete, View, Export, etc.)
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Módulo o página donde se realizó la acción
    /// </summary>
    public string Module { get; set; } = string.Empty;

    /// <summary>
    /// Descripción detallada de la acción
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Fecha y hora de la acción
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Dirección IP del usuario (opcional)
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// Datos adicionales en formato JSON (opcional)
    /// </summary>
    public string? AdditionalData { get; set; }
}

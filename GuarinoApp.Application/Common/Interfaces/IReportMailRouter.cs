namespace GuarinoApp.Application.Common.Interfaces;

/// <summary>
/// Resuelve a quién van los reportes que se envían desde la interfaz:
/// al usuario que inició sesión, con copia al administrador.
/// Si el usuario todavía no registró su correo, todo va al administrador.
/// </summary>
public interface IReportMailRouter
    {
    /// <summary>Correo del administrador (Notificaciones:EmailAdmin).</summary>
    string AdminEmail { get; }

    /// <summary>Destinatarios usando el correo del usuario logueado.</summary>
    (string Para, string? Copia) GetRecipients();

    /// <summary>Destinatarios para una dirección elegida a mano (diálogos).</summary>
    (string Para, string? Copia) ForRecipient(string email);
    }

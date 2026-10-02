namespace GuarinoApp.Infrastructure.Services;

/// <summary>
/// Notificaciones de operación SOLO para el administrador de la app.
/// Sección "Notificaciones" de appsettings.json.
/// El destinatario es único y fijo (EmailAdmin): ningún usuario ni ninguna
/// pantalla pueden modificarlo ni recibir estos correos.
/// </summary>
public class NotificacionesOptions
    {
    public const string SectionName = "Notificaciones";

    /// <summary>Activa los reportes de operación por correo.</summary>
    public bool Habilitadas { get; set; } = true;

    /// <summary>ÚNICO destinatario de los reportes (el dueño de la app).</summary>
    public string EmailAdmin { get; set; } = "adriansil@hotmail.com";

    /// <summary>Minutos entre revisiones de novedades (errores, salud, actualizaciones).</summary>
    public int IntervaloMinutos { get; set; } = 15;

    /// <summary>Asunto del reporte; se le agrega la fecha y hora al final:
    /// "Guarino App - logs del día : 2026-10-02 15:42".</summary>
    public string Asunto { get; set; } = "Guarino App - logs del día : ";
    }

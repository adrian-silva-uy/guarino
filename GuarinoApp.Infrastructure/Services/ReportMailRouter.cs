using GuarinoApp.Application.Common.Interfaces;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace GuarinoApp.Infrastructure.Services
    {
    /// <summary>
    /// Resuelve los destinatarios de los reportes exportados desde la interfaz:
    /// Para: el correo del usuario que inició sesión (claim "email", cargado
    ///       desde AppUser.Email al abrir la sesión).
    /// Copia: el administrador (Notificaciones:EmailAdmin), salvo que el
    ///        destinatario ya sea el admin (se evita mandarse copia a sí mismo).
    /// Si el usuario no registró su correo, el reporte va directo al admin.
    /// </summary>
    public class ReportMailRouter : IReportMailRouter
        {
        private readonly ICurrentUserState _state;
        private readonly NotificacionesOptions _notificaciones;

        public ReportMailRouter(ICurrentUserState state, IOptions<NotificacionesOptions> notificaciones)
            {
            _state = state;
            _notificaciones = notificaciones.Value;
            }

        public string AdminEmail => _notificaciones.EmailAdmin;

        public (string Para, string? Copia) GetRecipients()
            {
            var usuario = _state.User?.FindFirst(ClaimTypes.Email)?.Value;
            return string.IsNullOrWhiteSpace(usuario)
                ? ForRecipient(AdminEmail)
                : ForRecipient(usuario);
            }

        public (string Para, string? Copia) ForRecipient(string email)
            {
            var destino = (email ?? string.Empty).Trim();
            if (destino.Length == 0)
                return (AdminEmail, null);

            var copia = string.Equals(destino, AdminEmail, StringComparison.OrdinalIgnoreCase)
                ? null
                : AdminEmail;
            return (destino, copia);
            }
        }
    }

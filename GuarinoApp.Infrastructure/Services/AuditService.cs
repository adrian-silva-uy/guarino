using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GuarinoApp.Infrastructure.Services;

/// <summary>
/// Implementación del servicio de auditoría.
/// Registra acciones de usuarios en el log de auditoría.
/// </summary>
public class AuditService : IAuditService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AuditService(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task LogAsync(string action, string module, string description, string? additionalData = null)
    {
        try
        {
            var logEntry = new GuarinoApp.Domain.Entities.LogEntry
            {
                UserName = _currentUser.UserName ?? "Sistema",
                Role = _currentUser.IsInRole("Admin") ? "Admin" :
                       _currentUser.IsInRole("Gerencia") ? "Gerencia" :
                       _currentUser.IsInRole("Contador") ? "Contador" :
                       _currentUser.IsInRole("Operador") ? "Operador" : "Usuario",
                Action = action,
                Module = module,
                Description = description,
                AdditionalData = additionalData,
                Timestamp = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _currentUser.UserName
            };

            _context.LogEntries.Add(logEntry);
            await _context.SaveChangesAsync();
        }
        catch
        {
            // No lanzar excepciones desde el servicio de auditoría para no interrumpir el flujo principal
        }
    }
}

using GuarinoApp.Application.Common;
using GuarinoApp.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace GuarinoApp.Infrastructure.Services;

/// <summary>
/// Implementación del servicio de auditoría.
/// Registra acciones de usuarios en el log de auditoría.
/// Usa un scope propio por registro: se invoca desde handlers (que ya corren
/// dentro de la puerta de serialización) y también desde páginas, así que con
/// el DbContext del circuito compartido podía solaparse y perderse la entrada.
/// </summary>
public class AuditService : IAuditService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICurrentUserService _currentUser;

    public AuditService(
        IServiceScopeFactory scopeFactory,
        ICurrentUserService currentUser)
    {
        _scopeFactory = scopeFactory;
        _currentUser = currentUser;
    }

    public async Task LogAsync(string action, string module, string description, string? additionalData = null)
    {
        try
        {
            var logEntry = new GuarinoApp.Domain.Entities.LogEntry
            {
                UserName = _currentUser.UserName ?? "Sistema",
                Role = _currentUser.IsInRole(RoleNames.Admin) ? RoleNames.Admin :
                       _currentUser.IsInRole(RoleNames.Gerencia) ? RoleNames.Gerencia :
                       _currentUser.IsInRole(RoleNames.Contador) ? RoleNames.Contador :
                       _currentUser.IsInRole(RoleNames.Operador) ? RoleNames.Operador : "Usuario",
                Action = action,
                Module = module,
                Description = description,
                AdditionalData = additionalData,
                Timestamp = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _currentUser.UserName
            };

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            context.LogEntries.Add(logEntry);
            await context.SaveChangesAsync();
        }
        catch
        {
            // No lanzar excepciones desde el servicio de auditoría para no interrumpir el flujo principal
        }
    }
}

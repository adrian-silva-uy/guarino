using GuarinoApp.Application.Common;
using GuarinoApp.Application.Common.Interfaces;
using MediatR;

namespace GuarinoApp.Application.Modules.Auditoria.Commands.LogAction;

public record LogActionCommand(
    string Action,
    string Module,
    string Description,
    string? AdditionalData = null
) : IRequest;

public class LogActionCommandHandler : IRequestHandler<LogActionCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public LogActionCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task Handle(LogActionCommand request, CancellationToken cancellationToken)
    {
        var logEntry = new GuarinoApp.Domain.Entities.LogEntry
        {
            UserName = _currentUser.UserName ?? "Sistema",
            Role = _currentUser.IsInRole(RoleNames.Admin) ? RoleNames.Admin :
                   _currentUser.IsInRole(RoleNames.Gerencia) ? RoleNames.Gerencia :
                   _currentUser.IsInRole(RoleNames.Contador) ? RoleNames.Contador :
                   _currentUser.IsInRole(RoleNames.Operador) ? RoleNames.Operador : "Usuario",
            Action = request.Action,
            Module = request.Module,
            Description = request.Description,
            AdditionalData = request.AdditionalData,
            Timestamp = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.UserName
        };

        _context.LogEntries.Add(logEntry);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

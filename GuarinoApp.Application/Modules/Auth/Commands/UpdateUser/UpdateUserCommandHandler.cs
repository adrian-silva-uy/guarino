using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.Auth.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GuarinoApp.Application.Modules.Auth.Commands.UpdateUser;

public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, AuthResultDto>
{
    private static readonly string[] ValidRoles = { "Gerencia", "Contador", "Operador" };

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public UpdateUserCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IAuditService audit)
    {
        _context = context;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<AuthResultDto> Handle(UpdateUserCommand request, CancellationToken ct)
    {
        if (!ValidRoles.Contains(request.Role))
            return AuthResultDto.Fail("El rol seleccionado no es válido.");

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId && !u.IsDeleted, ct);
        if (user == null)
            return AuthResultDto.Fail("El usuario no existe.");

        var isSelf = _currentUser.UserId == user.Id.ToString();
        if (isSelf && !request.IsActive)
            return AuthResultDto.Fail("No podés deshabilitar tu propia cuenta.");

        if (isSelf && user.Role != request.Role)
            return AuthResultDto.Fail("No podés cambiar tu propio rol. Pedile a otro usuario de Gerencia.");

        var cambios = new List<string>();
        if (user.Role != request.Role) cambios.Add($"rol {user.Role} → {request.Role}");
        if (user.IsActive != request.IsActive) cambios.Add(request.IsActive ? "habilitado" : "deshabilitado");

        user.Role = request.Role;
        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = _currentUser.UserName;
        await _context.SaveChangesAsync(ct);

        if (cambios.Count > 0)
        {
            await _audit.LogAsync(
                "UpdateUser",
                "Usuarios",
                $"Usuario {user.UserName}: {string.Join(", ", cambios)}.");
        }

        return AuthResultDto.Ok();
    }
}

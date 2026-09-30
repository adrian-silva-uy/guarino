using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.Auth.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GuarinoApp.Application.Modules.Auth.Commands.UpdateUser;

/// <summary>
/// Edición de usuarios con las reglas de una aplicación madura:
/// - Solo Admin activa/desactiva usuarios.
/// - Solo Admin otorga o quita el rol Admin; Gerencia Admin/Contador/Operador.
/// - Nadie se desactiva a sí mismo ni se cambia su propio rol.
/// - Nunca se puede quitar o desactivar al último Admin activo.
/// - Cada cambio de estado registra desde cuándo está activo/inactivo.
/// - Concurrencia optimista: si otra sesión editó la fila, no se pisa el cambio.
/// </summary>
public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, AuthResultDto>
{
    private const string AdminRole = "Admin";
    private static readonly string[] ValidRoles = { "Admin", "Gerencia", "Contador", "Operador" };

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

        // Identidad propia: se compara por nombre de usuario porque funciona
        // igual en modo Local (login) y modo Windows (Active Directory).
        var isSelf = string.Equals(_currentUser.UserName, user.UserName, StringComparison.OrdinalIgnoreCase);
        if (isSelf && !request.IsActive)
            return AuthResultDto.Fail("No podés deshabilitar tu propia cuenta.");

        if (isSelf && user.Role != request.Role)
            return AuthResultDto.Fail("No podés cambiar tu propio rol. Pedile a otro usuario con permisos.");

        var isAdmin = _currentUser.IsInRole(AdminRole);
        var isGerencia = _currentUser.IsInRole("Gerencia");

        // Activar/desactivar: potestad exclusiva del Admin.
        var statusChanged = user.IsActive != request.IsActive;
        if (statusChanged && !isAdmin)
            return AuthResultDto.Fail("Solo un Administrador puede activar o desactivar usuarios.");

        var roleChanged = !string.Equals(user.Role, request.Role, StringComparison.Ordinal);

        // Cambiar roles: Admin y Gerencia.
        if (roleChanged && !isAdmin && !isGerencia)
            return AuthResultDto.Fail("No tenés permiso para cambiar roles de usuario.");

        // Asignar o quitar el rol Admin: exclusivo del Admin.
        if ((roleChanged && (user.Role == AdminRole || request.Role == AdminRole)) && !isAdmin)
            return AuthResultDto.Fail("Solo un Administrador puede asignar o quitar el rol Admin.");

        // Jamás dejar la aplicación sin un Admin activo.
        var losesAdminOrIsDeactivated =
            (user.Role == AdminRole && request.Role != AdminRole) || !request.IsActive;
        if (user.Role == AdminRole && losesAdminOrIsDeactivated)
        {
            var otherActiveAdmin = await _context.Users.AnyAsync(u =>
                u.Id != user.Id
                && !u.IsDeleted
                && u.Role == AdminRole
                && u.IsActive, ct);

            if (!otherActiveAdmin)
                return AuthResultDto.Fail("No se puede quitar o desactivar al último Administrador activo.");
        }

        var cambios = new List<string>();
        if (roleChanged)
            cambios.Add($"rol {user.Role} → {request.Role}");

        // Transiciones de estado con fecha "desde cuándo".
        if (statusChanged)
        {
            if (request.IsActive)
            {
                user.ActivatedAt = DateTime.UtcNow;
                user.DeactivatedAt = null;
                cambios.Add(
                    $"activo desde {user.ActivatedAt.Value.ToLocalTime():dd/MM/yyyy HH:mm}");
            }
            else
            {
                user.DeactivatedAt = DateTime.UtcNow;
                cambios.Add(
                    $"inactivo desde {user.DeactivatedAt.Value.ToLocalTime():dd/MM/yyyy HH:mm}");
            }
        }

        user.Role = request.Role;
        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = _currentUser.UserName;

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Otra sesión modificó el usuario mientras se editaba:
            // no pisar su cambio; se informa para que se recargue.
            return AuthResultDto.Fail(
                "Este usuario fue modificado por otra sesión. Recargá la página e intentá de nuevo.");
        }

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

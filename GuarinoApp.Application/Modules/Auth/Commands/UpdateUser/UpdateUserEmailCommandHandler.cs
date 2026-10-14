using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.Auth.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GuarinoApp.Application.Modules.Auth.Commands.UpdateUser;

/// <summary>
/// Edición del correo de un usuario con las mismas reglas que el resto del
/// módulo de usuarios:
/// - Solo Admin cambia correos (validado server-side).
/// - Formato de correo válido (o vacío para quitarlo).
/// - Concurrencia optimista: si otra sesión editó la fila, no se pisa.
/// - Alta auditada (sin registrar el valor anterior por privacidad mínima).
/// </summary>
public class UpdateUserEmailCommandHandler : IRequestHandler<UpdateUserEmailCommand, AuthResultDto>
{
    private const string AdminRole = "Admin";

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public UpdateUserEmailCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IAuditService audit)
    {
        _context = context;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<AuthResultDto> Handle(UpdateUserEmailCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsInRole(AdminRole))
            return AuthResultDto.Fail("Solo un Administrador puede modificar correos de usuarios.");

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId && !u.IsDeleted, ct);
        if (user == null)
            return AuthResultDto.Fail("El usuario no existe.");

        var email = (request.Email ?? string.Empty).Trim();
        if (email.Length > 0 && !EsCorreoValido(email))
            return AuthResultDto.Fail("El correo electrónico no es válido.");

        var nuevo = email.Length == 0 ? null : email;
        if (string.Equals(user.Email, nuevo, StringComparison.OrdinalIgnoreCase))
            return AuthResultDto.Ok(); // sin cambios: nada que guardar ni auditar

        user.Email = nuevo;
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = _currentUser.UserName;

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return AuthResultDto.Fail(
                "Este usuario fue modificado por otra sesión. Recargá la página e intentá de nuevo.");
        }

        await _audit.LogAsync(
            "UpdateUser",
            "Usuarios",
            nuevo == null
                ? $"Usuario {user.UserName}: correo eliminado; sus reportes irán al administrador."
                : $"Usuario {user.UserName}: correo actualizado.");

        return AuthResultDto.Ok();
    }

    /// <summary>Formato básico de correo (vacío se maneja aparte: es opcional).</summary>
    private static bool EsCorreoValido(string email)
    {
        try
        {
            _ = new System.Net.Mail.MailAddress(email);
            return true;
        }
        catch (System.FormatException)
        {
            return false;
        }
    }
}

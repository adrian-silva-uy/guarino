using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.Auth.Dtos;
using GuarinoApp.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GuarinoApp.Application.Modules.Auth.Commands.CreateUser;

/// <summary>
/// Alta de usuarios con las mismas reglas de madurez que la edición:
/// - Solo Admin crea usuarios (validado server-side, no solo en la UI).
/// - Contraseña hasheada con PBKDF2: jamás se persiste en texto plano.
/// - Usuario único, nombre completo obligatorio y rol activo de la tabla Roles.
/// - Alta auditada (quién creó a quién y con qué rol).
/// </summary>
public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, AuthResultDto>
{
    private const string AdminRole = "Admin";

    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public CreateUserCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        ICurrentUserService currentUser,
        IAuditService audit)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<AuthResultDto> Handle(CreateUserCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsInRole(AdminRole))
            return AuthResultDto.Fail("Solo un Administrador puede crear usuarios.");

        // El rol se valida contra la tabla Roles: la BD es la única fuente de verdad.
        var role = await _context.Roles
            .FirstOrDefaultAsync(r => r.Name == request.Role && r.IsActive && !r.IsDeleted, ct);
        if (role == null)
            return AuthResultDto.Fail("El rol seleccionado no es válido.");

        var userName = (request.UserName ?? string.Empty).Trim();
        var fullName = (request.FullName ?? string.Empty).Trim();

        if (userName.Length < 3)
            return AuthResultDto.Fail("El usuario debe tener al menos 3 caracteres.");

        if (string.IsNullOrWhiteSpace(fullName))
            return AuthResultDto.Fail("Ingresá el nombre completo.");

        var password = request.Password ?? string.Empty;
        if (password.Length < 6)
            return AuthResultDto.Fail("La contraseña debe tener al menos 6 caracteres.");

        var exists = await _context.Users
            .AnyAsync(u => u.UserName == userName && !u.IsDeleted, ct);
        if (exists)
            return AuthResultDto.Fail("Ese usuario ya existe. Elegí otro.");

        var user = new AppUser
        {
            UserName = userName,
            FullName = fullName,
            PasswordHash = _passwordHasher.Hash(password),
            RoleId = role.Id,
            IsActive = true,
            ActivatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.UserName
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);

        await _audit.LogAsync(
            "CreateUser",
            "Usuarios",
            $"Alta de usuario {user.UserName} con rol {role.Name} por {_currentUser.UserName}.");

        return AuthResultDto.Ok();
    }
}

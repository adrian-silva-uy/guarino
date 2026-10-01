using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.Auth.Dtos;
using GuarinoApp.Application.Settings;
using GuarinoApp.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GuarinoApp.Application.Modules.Auth.Commands.Register;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, AuthSessionDto>
{
    /// <summary>Rol del primer usuario registrado: el super-usuario de la aplicación.</summary>
    private const string AdminRole = "Admin";

    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuthTokenService _tokenService;
    private readonly IAuditService _audit;
    private readonly AuthSettings _authSettings;

    public RegisterCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IAuthTokenService tokenService,
        IAuditService audit,
        IOptions<AuthSettings> authSettings)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _audit = audit;
        _authSettings = authSettings.Value;
    }

    public async Task<AuthSessionDto> Handle(RegisterCommand request, CancellationToken ct)
    {
        if (!_authSettings.AllowRegistration)
            return AuthSessionDto.Fail("El registro de usuarios está deshabilitado. Contactá al administrador.");

        var userName = request.UserName.Trim();
        var fullName = request.FullName.Trim();

        if (userName.Length < 3)
            return AuthSessionDto.Fail("El usuario debe tener al menos 3 caracteres.");

        if (string.IsNullOrWhiteSpace(fullName))
            return AuthSessionDto.Fail("Ingresá tu nombre completo.");

        var password = request.Password ?? string.Empty;
        if (password.Length < 6)
            return AuthSessionDto.Fail("La contraseña debe tener al menos 6 caracteres.");

        var exists = await _context.Users
            .AnyAsync(u => u.UserName == userName && !u.IsDeleted, ct);
        if (exists)
            return AuthSessionDto.Fail("Ese usuario ya existe. Elegí otro.");

        // El primer usuario de la organización siempre queda como Admin.
        var hasUsers = await _context.Users.AnyAsync(u => !u.IsDeleted, ct);
        var roleName = hasUsers ? _authSettings.DefaultRole : AdminRole;

        // El rol se resuelve contra la tabla Roles (fuente única de verdad).
        var role = await _context.Roles
            .FirstOrDefaultAsync(r => r.Name == roleName && r.IsActive && !r.IsDeleted, ct);
        if (role == null)
            return AuthSessionDto.Fail("La configuración de roles está incompleta. Contactá al soporte.");

        var user = new AppUser
        {
            UserName = userName,
            FullName = fullName,
            PasswordHash = _passwordHasher.Hash(password),
            RoleId = role.Id,
            IsActive = true,
            ActivatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);

        var token = _tokenService.Issue(new AuthTokenPayload
        {
            UserName = user.UserName,
            FullName = user.FullName,
            Role = role.Name,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(Math.Max(1, _authSettings.TokenValidityHours))
        });

        await _audit.LogAsync(
            "Register",
            "Acceso",
            $"Alta de usuario {user.UserName} con rol {role.Name}.");

        return new AuthSessionDto
        {
            Success = true,
            UserName = user.UserName,
            FullName = user.FullName,
            Role = role.Name,
            Token = token
        };
    }
}

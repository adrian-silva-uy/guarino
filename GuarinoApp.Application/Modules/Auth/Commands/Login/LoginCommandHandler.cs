using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.Auth.Dtos;
using GuarinoApp.Application.Settings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GuarinoApp.Application.Modules.Auth.Commands.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, AuthSessionDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuthTokenService _tokenService;
    private readonly IAuditService _audit;
    private readonly AuthSettings _authSettings;

    public LoginCommandHandler(
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

    public async Task<AuthSessionDto> Handle(LoginCommand request, CancellationToken ct)
    {
        var userName = request.UserName.Trim();
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.UserName == userName && !u.IsDeleted, ct);

        if (user == null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            await _audit.LogAsync(
                "LoginFailed",
                "Acceso",
                $"Inicio de sesión fallido para '{userName}'.");
            return AuthSessionDto.Fail("Usuario o contraseña incorrectos.");
        }

        if (!user.IsActive)
        {
            await _audit.LogAsync(
                "LoginFailed",
                "Acceso",
                $"Intento de acceso de usuario deshabilitado '{userName}'.");
            return AuthSessionDto.Fail("Tu usuario está deshabilitado. Contactá al administrador.");
        }

        // Registro de actividad del acceso: es best-effort, no debe impedir
        // el inicio de sesión si hay una colación con otra actualización.
        try
        {
            user.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Otra sesión actualizó el usuario al mismo tiempo: no bloquear el ingreso.
        }

        var token = _tokenService.Issue(new AuthTokenPayload
        {
            UserName = user.UserName,
            FullName = user.FullName,
            Role = user.Role,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(Math.Max(1, _authSettings.TokenValidityHours))
        });

        await _audit.LogAsync(
            "Login",
            "Acceso",
            $"Inicio de sesión de {user.UserName} (rol {user.Role}).");

        return new AuthSessionDto
        {
            Success = true,
            UserName = user.UserName,
            FullName = user.FullName,
            Role = user.Role,
            Token = token
        };
    }
}

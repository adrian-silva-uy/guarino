using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.Auth.Dtos;
using GuarinoApp.Application.Modules.Auth.Services;
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
    private readonly ControlIntentosLogin _intentosLogin;

    public LoginCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IAuthTokenService tokenService,
        IAuditService audit,
        IOptions<AuthSettings> authSettings,
        ControlIntentosLogin intentosLogin)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _audit = audit;
        _authSettings = authSettings.Value;
        _intentosLogin = intentosLogin;
    }

    public async Task<AuthSessionDto> Handle(LoginCommand request, CancellationToken ct)
    {
        var userName = request.UserName.Trim();

        // Bloqueo temporal tras fallos consecutivos (fuerza bruta): mientras
        // dure, ni siquiera se consulta la base de datos.
        if (_intentosLogin.EstaBloqueado(userName, out var liberacionUtc))
        {
            await _audit.LogAsync(
                "LoginBlocked",
                "Acceso",
                $"Intento de acceso a '{userName}' durante bloqueo temporal (liberación {liberacionUtc:u}).");
            return AuthSessionDto.Fail(
                "Demasiados intentos fallidos: la cuenta está bloqueada temporalmente. Esperá unos minutos y volvé a intentar.");
        }

        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserName == userName && !u.IsDeleted, ct);

        if (user == null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            var bloqueada = _intentosLogin.RegistrarFallo(userName);
            await _audit.LogAsync(
                "LoginFailed",
                "Acceso",
                bloqueada
                    ? $"Inicio de sesión fallido para '{userName}': cuenta bloqueada {ControlIntentosLogin.BloqueoMinutos} min tras {ControlIntentosLogin.MaximoIntentos} intentos."
                    : $"Inicio de sesión fallido para '{userName}'.");
            return AuthSessionDto.Fail(bloqueada
                ? "Usuario o contraseña incorrectos. Por seguridad, la cuenta queda bloqueada unos minutos."
                : "Usuario o contraseña incorrectos.");
        }

        if (!user.IsActive)
        {
            await _audit.LogAsync(
                "LoginFailed",
                "Acceso",
                $"Intento de acceso de usuario deshabilitado '{userName}'.");
            return AuthSessionDto.Fail("Tu usuario está deshabilitado. Contactá al administrador.");
        }

        // Ingreso válido: borra los fallos previos acumulados de esta cuenta.
        _intentosLogin.RegistrarExito(userName);

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
            Role = user.Role.Name,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(Math.Max(1, _authSettings.TokenValidityHours))
        });

        await _audit.LogAsync(
            "Login",
            "Acceso",
            $"Inicio de sesión de {user.UserName} (rol {user.Role.Name}).");

        return new AuthSessionDto
        {
            Success = true,
            UserName = user.UserName,
            FullName = user.FullName,
            Role = user.Role.Name,
            Token = token
        };
    }
}

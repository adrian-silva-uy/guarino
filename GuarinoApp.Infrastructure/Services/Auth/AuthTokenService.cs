using GuarinoApp.Application.Common.Interfaces;
using Microsoft.AspNetCore.DataProtection;
using System.Text.Json;

namespace GuarinoApp.Infrastructure.Services.Auth;

/// <summary>
/// Emite y valida tokens de sesión con Data Protection de ASP.NET Core:
/// el payload se serializa y se protege con las claves del servidor, de modo
/// que no puede alterarse desde el navegador. Si las claves rotan o el token
/// se manipula, la validación falla y el usuario vuelve a ingresar.
/// </summary>
public sealed class AuthTokenService : IAuthTokenService
{
    private const string ProtectorPurpose = "GuarinoApp.Auth.v1";

    private readonly IDataProtector _protector;

    public AuthTokenService(IDataProtectionProvider dataProtectionProvider)
        => _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);

    public string Issue(AuthTokenPayload payload)
        => _protector.Protect(JsonSerializer.Serialize(payload));

    public AuthTokenPayload? Validate(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        try
        {
            var payload = JsonSerializer.Deserialize<AuthTokenPayload>(_protector.Unprotect(token));
            if (payload == null || string.IsNullOrWhiteSpace(payload.UserName))
                return null;

            if (payload.ExpiresAtUtc < DateTime.UtcNow)
                return null;

            return payload;
        }
        catch
        {
            // Token manipulado, con claves ajenas o con formato inválido.
            return null;
        }
    }
}

using System.Security.Claims;
using GuarinoApp.Application.Common.Interfaces;

namespace GuarinoApp.Web.Mcp;

/// <summary>
/// Autenticación del endpoint MCP (/mcp): lee el token de sesión de la app en el
/// encabezado «Authorization: Bearer &lt;token&gt;» (el mismo token firmado con
/// Data Protection que emite IAuthTokenService al iniciar sesión y se guarda en
/// localStorage), lo valida y compone el usuario con su rol real. Así las tools
/// MCP ven el perfil del usuario vía ICurrentUserService (que en requests HTTP
/// usa HttpContext.User) y aplican la misma matriz de módulos que la app.
/// Sin token válido responde 401.
/// </summary>
public sealed class McpAuthMiddleware
{
    private const string PrefijoBearer = "Bearer ";

    private readonly RequestDelegate _next;
    private readonly ILogger<McpAuthMiddleware> _logger;

    public McpAuthMiddleware(RequestDelegate next, ILogger<McpAuthMiddleware> logger)
        => (_next, _logger) = (next, logger);

    public async Task InvokeAsync(HttpContext context, IAuthTokenService tokenService)
    {
        var header = context.Request.Headers.Authorization.ToString();
        if (header.Length > PrefijoBearer.Length
            && header.StartsWith(PrefijoBearer, StringComparison.OrdinalIgnoreCase))
        {
            var payload = tokenService.Validate(header[PrefijoBearer.Length..].Trim());
            if (payload is not null && payload.ExpiresAtUtc > DateTime.UtcNow)
            {
                var claims = new List<Claim>
                {
                    new(ClaimTypes.Name, payload.UserName),
                    new(ClaimTypes.Role, payload.Role),
                    new("full_name", payload.FullName)
                };
                context.User = new ClaimsPrincipal(
                    new ClaimsIdentity(claims, authenticationType: "GuarinoMcp"));
                await _next(context);
                return;
            }
        }

        _logger.LogWarning(
            "MCP: request al endpoint /mcp sin token de sesión válido (origen {IP}).",
            context.Connection.RemoteIpAddress);

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate = "Bearer";
        context.Response.ContentType = "text/plain; charset=utf-8";
        await context.Response.WriteAsync(
            "Autenticación requerida: envíe el encabezado 'Authorization: Bearer <token>' con el token de sesión de la aplicación.",
            context.RequestAborted);
    }
}

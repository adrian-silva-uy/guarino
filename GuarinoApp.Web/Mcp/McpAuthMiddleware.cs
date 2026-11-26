using System.Security.Claims;
using GuarinoApp.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GuarinoApp.Web.Mcp;

/// <summary>
/// Autenticación del endpoint MCP (/mcp): valida el token de sesión y vuelve
/// a comprobar en base de datos que el usuario siga activo y que su rol vigente
/// continúe habilitado. El rol se toma de la base, no del token potencialmente
/// desactualizado.
/// </summary>
public sealed class McpAuthMiddleware
{
    private const string PrefijoBearer = "Bearer ";

    private readonly RequestDelegate _next;
    private readonly ILogger<McpAuthMiddleware> _logger;

    public McpAuthMiddleware(RequestDelegate next, ILogger<McpAuthMiddleware> logger)
        => (_next, _logger) = (next, logger);

    public async Task InvokeAsync(
        HttpContext context,
        IAuthTokenService tokenService,
        IApplicationDbContext dbContext)
    {
        var header = context.Request.Headers.Authorization.ToString();
        if (header.Length > PrefijoBearer.Length
            && header.StartsWith(PrefijoBearer, StringComparison.OrdinalIgnoreCase))
        {
            var payload = tokenService.Validate(header[PrefijoBearer.Length..].Trim());
            if (payload is not null && payload.ExpiresAtUtc > DateTime.UtcNow)
            {
                var user = await dbContext.Users
                    .AsNoTracking()
                    .Include(u => u.Role)
                    .FirstOrDefaultAsync(
                        u => u.UserName == payload.UserName && !u.IsDeleted,
                        context.RequestAborted);

                if (user is { IsActive: true, Role.IsActive: true })
                {
                    var claims = new List<Claim>
                    {
                        new(ClaimTypes.Name, user.UserName),
                        new(ClaimTypes.Role, user.Role.Name),
                        new("full_name", user.FullName)
                    };

                    context.User = new ClaimsPrincipal(
                        new ClaimsIdentity(claims, authenticationType: "GuarinoMcp"));
                    await _next(context);
                    return;
                }
            }
        }

        _logger.LogWarning(
            "MCP: request al endpoint /mcp sin sesión vigente (origen {IP}).",
            context.Connection.RemoteIpAddress);

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate = "Bearer";
        context.Response.ContentType = "text/plain; charset=utf-8";
        await context.Response.WriteAsync(
            "Autenticación requerida: envíe un token de sesión vigente.",
            context.RequestAborted);
    }
}

using System.Security.Claims;
using GuarinoApp.Application.Common;

namespace GuarinoApp.Tests;

public static class TestUsersFixture
{
    public static IReadOnlyDictionary<string, ClaimsPrincipal> All { get; } =
        RoleNames.Todos.ToDictionary(role => role, Create);

    public static ClaimsPrincipal Create(string role) => new(
        new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.Name, $"demo-{role.ToLowerInvariant()}"),
                new Claim(ClaimTypes.Role, role)
            },
            authenticationType: "Testing"));
}

using System.Security.Claims;
using GuarinoApp.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GuarinoApp.Tests;

[TestClass]
public sealed class RoleAuthorizationTests
{
    [DataTestMethod]
    [DataRow("Admin")]
    [DataRow("Gerencia")]
    [DataRow("Contador")]
    [DataRow("Operador")]
    public void TodosAutorizacion_AcceptsEverySupportedRole(string role)
    {
        var policy = new AuthorizationPolicyBuilder()
            .RequireRole(RoleNames.TodosAutorizacion.Split(", "))
            .Build();
        var principal = PrincipalFor(role);

        Assert.IsTrue(policy.Requirements.OfType<RolesAuthorizationRequirement>().Single().AllowedRoles.Contains(role));
        Assert.IsTrue(principal.IsInRole(role));
    }

    [DataTestMethod]
    [DataRow("Admin", true)]
    [DataRow("Gerencia", true)]
    [DataRow("Contador", true)]
    [DataRow("Operador", false)]
    public void Financieros_AcceptsOnlyFinancialRoles(string role, bool expected)
    {
        var allowed = RoleNames.Financieros.Contains(role);
        Assert.AreEqual(expected, allowed);
    }

    [DataTestMethod]
    [DataRow("Admin", true)]
    [DataRow("Gerencia", true)]
    [DataRow("Contador", false)]
    [DataRow("Operador", false)]
    public void Administracion_AcceptsOnlyAdministrativeRoles(string role, bool expected)
    {
        var allowed = RoleNames.Administracion.Contains(role);
        Assert.AreEqual(expected, allowed);
    }

    private static ClaimsPrincipal PrincipalFor(string role) => new(
        new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.Name, $"demo-{role.ToLowerInvariant()}"),
                new Claim(ClaimTypes.Role, role)
            },
            authenticationType: "Testing"));
}

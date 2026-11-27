using System.Reflection;
using GuarinoApp.Application.Common;
using GuarinoApp.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GuarinoApp.Tests;

[TestClass]
public sealed class RouteCoverageTests
{
    [TestMethod]
    public void EveryRoutedPage_HasAnExplicitAuthorizationContract_AndRouteMapMatchesIt()
    {
        var routes = typeof(global::Program).Assembly.GetTypes()
            .SelectMany(type => type.GetCustomAttributes<RouteAttribute>(inherit: false)
                .Select(route => (Type: type, Route: Normalize(route.Template))))
            .ToArray();
        var accessMap = new MapaAccesosRutas();

        Assert.IsTrue(routes.Length >= 37, $"Se encontraron sólo {routes.Length} rutas; revisar la cobertura de páginas.");

        foreach (var (type, route) in routes)
        {
            var authorize = type.GetCustomAttribute<AuthorizeAttribute>(inherit: true);
            if (route is "/login" or "/register")
            {
                Assert.IsNull(authorize, $"La página pública {route} debe seguir siendo accesible sin sesión.");
                continue;
            }

            Assert.IsNotNull(authorize, $"La ruta {route} no declara @attribute [Authorize].");
            var allowedRoles = (authorize!.Roles ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            Assert.IsTrue(allowedRoles.Length > 0, $"La ruta {route} no limita el acceso a roles conocidos.");
            Assert.IsTrue(allowedRoles.All(RoleNames.Todos.Contains), $"La ruta {route} contiene un rol desconocido.");

            foreach (var role in RoleNames.Todos)
                Assert.AreEqual(allowedRoles.Contains(role), accessMap.Permite(route, role),
                    $"El mapa de navegación y el atributo Authorize divergen en {route} para {role}.");

            Assert.IsFalse(accessMap.Permite(route, null), $"La ruta protegida {route} permite navegación sin sesión.");
        }
    }

    private static string Normalize(string route)
    {
        var value = route.Trim();
        return value.StartsWith('/') ? value : $"/{value}";
    }
}

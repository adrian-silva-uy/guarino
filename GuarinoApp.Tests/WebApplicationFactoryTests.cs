using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GuarinoApp.Tests;

[TestClass]
public sealed class WebApplicationFactoryTests
{
    [DataTestMethod]
    [DataRow("Admin")]
    [DataRow("Gerencia")]
    [DataRow("Contador")]
    [DataRow("Operador")]
    public async Task RootEndpoint_StartsForEveryRole(string role)
    {
        await using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-Role", role);

        using var response = await client.GetAsync("/");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        StringAssert.Contains(await response.Content.ReadAsStringAsync(), "Guarino");
    }

    [TestMethod]
    public async Task HealthEndpoints_AreReachableInControlledHost()
    {
        await using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var health = await client.GetAsync("/health");
        var ready = await client.GetAsync("/health/ready");

        Assert.AreEqual(HttpStatusCode.OK, health.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, ready.StatusCode);
    }

    private sealed class TestApplicationFactory : WebApplicationFactory<global::Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Notificaciones:Habilitadas", "false");
            builder.UseSetting("Ollama:Enabled", "false");
            builder.UseSetting("IaWeb:Habilitada", "false");
        }
    }
}

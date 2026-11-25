using System.Diagnostics;
using System.Net;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GuarinoApp.Tests;

[TestClass]
public sealed class PlaywrightE2ETests
{
    [TestMethod]
    public async Task DemoCompleta_PorCadaRol()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        await using var server = await DemoServer.StartAsync();

        foreach (var role in new[] { "Admin", "Gerencia", "Contador", "Operador" })
        {
            await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                ExtraHTTPHeaders = new Dictionary<string, string> { ["X-Test-Role"] = role }
            });
            var page = await context.NewPageAsync();
            var response = await page.GotoAsync(server.Url);

            Assert.IsNotNull(response);
            Assert.AreEqual((int)HttpStatusCode.OK, response.Status);
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
            Assert.IsTrue(await page.Locator("body").CountAsync() > 0, $"No se renderizó el documento para {role}.");
            StringAssert.Contains(await page.ContentAsync(), "blazor", $"La aplicación Blazor no se cargó para {role}.");
        }
    }

    private sealed class DemoServer : IAsyncDisposable
    {
        private readonly Process _process;
        public string Url { get; }

        private DemoServer(Process process, string url) => (_process, Url) = (process, url);

        public static async Task<DemoServer> StartAsync()
        {
            var port = GetFreePort();
            var project = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "GuarinoApp.Web", "GuarinoApp.Web.csproj"));
            var info = new ProcessStartInfo("dotnet", $"run --project \"{project}\" --no-launch-profile -- --urls http://127.0.0.1:{port}")
            {
                WorkingDirectory = Path.GetDirectoryName(project)!,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            info.Environment["ASPNETCORE_ENVIRONMENT"] = "Testing";
            var process = Process.Start(info) ?? throw new InvalidOperationException("No se pudo iniciar GuarinoApp.Web.");
            var url = $"http://127.0.0.1:{port}/";
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

            for (var attempt = 0; attempt < 120; attempt++)
            {
                if (process.HasExited)
                    throw new InvalidOperationException($"El servidor E2E terminó: {await process.StandardError.ReadToEndAsync()}");
                try
                {
                    using var response = await client.GetAsync(url);
                    if ((int)response.StatusCode < 500)
                        return new DemoServer(process, url);
                }
                catch (HttpRequestException) { }
                catch (TaskCanceledException) { }
                await Task.Delay(250);
            }

            process.Kill(true);
            throw new TimeoutException("El servidor E2E no estuvo disponible.");
        }

        private static int GetFreePort()
        {
            using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        }

        public async ValueTask DisposeAsync()
        {
            if (!_process.HasExited)
            {
                _process.Kill(true);
                await _process.WaitForExitAsync();
            }
            _process.Dispose();
        }
    }
}

using System.Diagnostics;
using System.Net;
using System.Reflection;
using GuarinoApp.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GuarinoApp.Tests;

[TestClass]
public sealed class PlaywrightE2ETests
{
    [TestMethod]
    public async Task CadaRol_AbreInicio_YAdminRecorreTodasLasRutasProtegidas()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        await using var server = await DemoServer.StartAsync();

        var roles = RoleNames.Todos.ToArray();
        var contexts = new List<IBrowserContext>();
        IPage? adminPage = null;
        var browserErrors = new List<string>();
        try
        {
            foreach (var role in roles)
            {
                var context = await browser.NewContextAsync(new BrowserNewContextOptions
                {
                    ExtraHTTPHeaders = new Dictionary<string, string> { ["X-Test-Role"] = role }
                });
                await context.AddCookiesAsync(new[]
                {
                    new Microsoft.Playwright.Cookie { Name = "test_role", Value = role, Url = server.Url }
                });
                contexts.Add(context);
                var page = await context.NewPageAsync();
                page.PageError += (_, error) => browserErrors.Add($"{role}: {error}");
                page.Console += (_, message) =>
                {
                    if (message.Type == "error")
                        browserErrors.Add($"{role}: {message.Text}");
                };
                var response = await page.GotoAsync(server.Url,
                    new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
                Assert.AreEqual((int)HttpStatusCode.OK, response?.Status);
                try
                {
                    await page.GetByText("Guarino App", new PageGetByTextOptions { Exact = true })
                        .WaitForAsync(new LocatorWaitForOptions { Timeout = 8_000 });
                }
                catch (TimeoutException)
                {
                    Assert.Fail($"No se montó el layout para {role}. URL={page.Url}; texto={await page.Locator("body").InnerTextAsync()}; errores={string.Join(" | ", browserErrors)}; logs={server.Logs}");
                }
                Assert.IsTrue(await page.Locator(".mud-main-content").InnerTextAsync() is { Length: > 0 },
                    $"El inicio no mostró contenido para {role}.");

                if (role == RoleNames.Admin)
                {
                    adminPage = page;
                    continue;
                }

                if (role == RoleNames.Gerencia)
                {
                    await VerificarAccesoAsync(page, server.Url, "/contactos", permitido: true);
                    await VerificarAccesoAsync(page, server.Url, "/usuarios", permitido: true);
                }
                else if (role == RoleNames.Contador)
                {
                    await VerificarAccesoAsync(page, server.Url, "/contaduria", permitido: true);
                    await VerificarAccesoAsync(page, server.Url, "/contactos", permitido: false);
                }
                else if (role == RoleNames.Operador)
                {
                    await VerificarAccesoAsync(page, server.Url, "/stockgeneral", permitido: true);
                    await VerificarAccesoAsync(page, server.Url, "/usuarios", permitido: false);
                    await VerificarAccesoAsync(page, server.Url, "/contaduria", permitido: false);
                }

                await context.CloseAsync();
            }

            // Navega cada página protegida desde el navegador real; espera el circuito
            // interactivo y comprueba contenido del área principal, no sólo el shell HTTP.
            var rutas = typeof(global::Program).Assembly.GetTypes()
                .SelectMany(type => type.GetCustomAttributes<RouteAttribute>(inherit: false)
                    .Select(route => (Type: type, Path: Normalize(route.Template))))
                .Where(item => item.Path is not "/login" and not "/register")
                .Where(item => item.Type.GetCustomAttribute<AuthorizeAttribute>(inherit: true) is not null)
                .Select(item => item.Path)
                .Where(path => path != "/")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            Assert.IsTrue(rutas.Length >= 37, $"Se encontraron sólo {rutas.Length} rutas protegidas.");

            Assert.IsNotNull(adminPage);
            foreach (var ruta in rutas)
            {
                var contenidoAnterior = (await adminPage.Locator(".mud-main-content").InnerTextAsync()).Trim();
                await adminPage.EvaluateAsync("path => { history.pushState({}, '', path); dispatchEvent(new PopStateEvent('popstate')); }", ruta);
                await adminPage.WaitForFunctionAsync(
                    "({path, previous}) => location.pathname === path && (document.querySelector('.mud-main-content')?.innerText || '').trim() !== previous",
                    new { path = ruta, previous = contenidoAnterior });
                await adminPage.Locator(".mud-main-content").WaitForAsync();
                var contenido = await adminPage.Locator(".mud-main-content").InnerTextAsync();
                Assert.IsTrue(contenido.Trim().Length > 20, $"La ruta {ruta} no renderizó contenido de módulo.");
                Assert.IsFalse(contenido.Contains("No tenés permiso para acceder", StringComparison.OrdinalIgnoreCase),
                    $"Admin recibió una denegación inesperada en {ruta}.");
                if (await adminPage.Locator("#blazor-error-ui").IsVisibleAsync())
                    Assert.Fail($"La ruta {ruta} produjo un error Blazor visible. URL={adminPage.Url}; errores navegador={string.Join(" | ", browserErrors)}; logs servidor={server.Logs}");
            }

        }
        finally
        {
            foreach (var context in contexts)
                await context.DisposeAsync();
        }
    }

    private static async Task VerificarAccesoAsync(IPage page, string baseUrl, string ruta, bool permitido)
    {
        await page.GotoAsync(new Uri(new Uri(baseUrl), ruta.TrimStart('/')).ToString(),
            new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
        var denegacion = page.GetByText("No tenés permiso para acceder a esta sección");
        if (permitido)
        {
            await page.Locator(".mud-main-content").WaitForAsync();
            Assert.AreEqual(0, await denegacion.CountAsync(), $"La ruta permitida {ruta} fue denegada.");
            Assert.IsTrue((await page.Locator(".mud-main-content").InnerTextAsync()).Trim().Length > 20,
                $"La ruta permitida {ruta} no renderizó contenido.");
        }
        else
        {
            await denegacion.WaitForAsync();
            Assert.IsTrue(await denegacion.IsVisibleAsync(), $"La ruta {ruta} no mostró denegación para el rol actual.");
        }
    }

    private static string Normalize(string route) => route.StartsWith('/') ? route : $"/{route}";

    private sealed class DemoServer : IAsyncDisposable
    {
        private readonly Process _process;
        private readonly System.Collections.Concurrent.ConcurrentQueue<string> _logs = new();
        public string Url { get; }
        public string Logs => string.Join(" | ", _logs);

        private DemoServer(Process process, string url, System.Collections.Concurrent.ConcurrentQueue<string> logs) =>
            (_process, Url, _logs) = (process, url, logs);

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
            info.Environment["Auth__Mode"] = "Windows";
            var logs = new System.Collections.Concurrent.ConcurrentQueue<string>();
            var process = new Process { StartInfo = info };
            process.OutputDataReceived += (_, args) => { if (args.Data is not null) logs.Enqueue(args.Data); };
            process.ErrorDataReceived += (_, args) => { if (args.Data is not null) logs.Enqueue(args.Data); };
            if (!process.Start())
                throw new InvalidOperationException("No se pudo iniciar GuarinoApp.Web.");
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
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
                        return new DemoServer(process, url, logs);
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

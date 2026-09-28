using Blazored.LocalStorage;
using GuarinoApp.Application;
using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Settings;
using GuarinoApp.Infrastructure;
using GuarinoApp.Infrastructure.Data;
using GuarinoApp.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.Extensions.Options;
using MudBlazor.Services;
using System.Globalization;

CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme).AddNegotiate();
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddMudServices();
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Caché en memoria (no se usa Redis por ahora; esta es la implementación
// en memoria de IDistributedCache incluida en el SDK de ASP.NET Core,
// no requiere ningún paquete NuGet adicional).
builder.Services.AddMemoryCache();
builder.Services.AddDistributedMemoryCache();

// Configurar ClimateRisk desde appsettings
builder.Services.Configure<ClimateRiskSettings>(
    builder.Configuration.GetSection(ClimateRiskSettings.SectionName));

// IClimateService con OpenMeteoClimateService
builder.Services.AddScoped<IClimateService, OpenMeteoClimateService>();

// Registrar servicio de Riesgo Climático
builder.Services.AddScoped<IClimateRiskService, ClimateRiskService>();

// Background service para detección automática
builder.Services.AddHostedService<ClimateDetectionBackgroundService>();

// HttpClient genérico
builder.Services.AddHttpClient();

// Opciones de Ollama
builder.Services.AddOptions<OllamaOptions>()
    .BindConfiguration(OllamaOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

// ============================================================================
// FIX CRÍTICO: Registrar el cliente named "Ollama" con timeout y base address
// ============================================================================
builder.Services.AddHttpClient("Ollama", (sp, client) =>
{
    var opts = sp.GetRequiredService<IOptionsMonitor<OllamaOptions>>().CurrentValue;

    if (!string.IsNullOrWhiteSpace(opts.Url))
        client.BaseAddress = new Uri(opts.Url);

    // Asegurar un timeout mínimo de 5 minutos (300s) para evitar TaskCanceledException
    // en el cold start del modelo o en generaciones largas.
    // Se respeta el valor de appsettings si es mayor a 300, sino se usa 300.
    var timeout = Math.Max(opts.TimeoutSeconds, 300);
    client.Timeout = TimeSpan.FromSeconds(timeout);
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
    PooledConnectionLifetime = TimeSpan.FromMinutes(10), // Reutilizar conexiones
    MaxConnectionsPerServer = 4 // Limitar concurrencia para no saturar CPU/GPU
    });

QuestPdfFontRegistrar.RegisterFonts();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    {
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    }

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

if (app.Environment.IsDevelopment())
    {
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    DbInitializer.Initialize(context);
    }

app.Run();

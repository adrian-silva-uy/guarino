using Blazored.LocalStorage;
using GuarinoApp.Application;
using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Settings;
using GuarinoApp.Infrastructure;
using GuarinoApp.Infrastructure.Data;
using GuarinoApp.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using MudBlazor.Services;
using System.Globalization;
using System.Threading.RateLimiting;

CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// AUTENTICACIÓN Y AUTORIZACIÓN
// Negotiate (Windows Auth) es correcto para app Blazor Server interna en red
// corporativa. JWT sería para APIs públicas o SPAs, no aplica aquí.
// ============================================================================
builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme).AddNegotiate();
builder.Services.AddAuthorization();

// ============================================================================
// SERVICIOS DE APLICACIÓN
// ============================================================================
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddMudServices();
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ============================================================================
// CACHÉ EN MEMORIA
// AddDistributedMemoryCache es la implementación en memoria de IDistributedCache
// incluida en el SDK de ASP.NET Core. Para un solo servidor es suficiente;
// Redis solo sería necesario en entorno distribuido con múltiples instancias.
// ============================================================================
builder.Services.AddMemoryCache();
builder.Services.AddDistributedMemoryCache();

// ============================================================================
// CONFIGURACIÓN VALIDADA
// ValidateOnStart asegura que la app no arranque con configuración inválida.
// ============================================================================
builder.Services.AddOptions<ClimateRiskSettings>()
    .Bind(builder.Configuration.GetSection(ClimateRiskSettings.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<EmailOptions>()
    .Bind(builder.Configuration.GetSection(EmailOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// ============================================================================
// SERVICIOS DE DOMINIO
// ============================================================================
builder.Services.AddScoped<IClimateService, OpenMeteoClimateService>();
builder.Services.AddScoped<IClimateRiskService, ClimateRiskService>();

// ============================================================================
// BACKGROUND SERVICES
// ClimateMonitoringHostedService es el servicio canónico de escaneo climático.
// ClimateDetectionBackgroundService fue removido: era una duplicación exacta
// de ClimateMonitoringHostedService (mismo intervalo, misma lógica).
// ============================================================================
builder.Services.AddHostedService<ClimateMonitoringHostedService>();

// ============================================================================
// HTTP CLIENT
// ============================================================================
builder.Services.AddHttpClient();

// ============================================================================
// OLLAMA - Cliente HTTP con timeout y base address configurables
// ============================================================================
builder.Services.AddOptions<OllamaOptions>()
    .BindConfiguration(OllamaOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHttpClient("Ollama", (sp, client) =>
{
    var opts = sp.GetRequiredService<IOptionsMonitor<OllamaOptions>>().CurrentValue;

    if (!string.IsNullOrWhiteSpace(opts.Url))
        client.BaseAddress = new Uri(opts.Url);

    // Timeout mínimo de 5 minutos (300s) para evitar TaskCanceledException
    // en el cold start del modelo o en generaciones largas.
    var timeout = Math.Max(opts.TimeoutSeconds, 300);
    client.Timeout = TimeSpan.FromSeconds(timeout);
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(10),
    MaxConnectionsPerServer = 4
});

// ============================================================================
// HEALTH CHECKS
// Monitoreo de SQL Server y Ollama para herramientas de infraestructura.
// ============================================================================
builder.Services.AddHealthChecks()
    .AddCheck<SqlServerHealthCheck>("sql-server")
    .AddCheck<OllamaHealthCheck>("ollama");

// ============================================================================
// RATE LIMITING
// Protección contra abuso de la API (especialmente endpoints de IA).
// ============================================================================
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("fixed", limiterOptions =>
    {
        limiterOptions.PermitLimit = 100;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 10;
    });
});

// ============================================================================
// RESPONSE COMPRESSION
// ============================================================================
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

// ============================================================================
// CORS - Configuración explícita para Blazor Server
// En producción, restringir a orígenes específicos.
// ============================================================================
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
        else
        {
            // En producción, configurar orígenes específicos desde appsettings
            var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
    });
});

// ============================================================================
// SEGURIDAD - Headers HTTP
// ============================================================================
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
    options.Preload = true;
});

QuestPdfFontRegistrar.RegisterFonts();

var app = builder.Build();

// ============================================================================
// PIPELINE DE HTTP
// ============================================================================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseResponseCompression();
app.UseStaticFiles();
app.UseRouting();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// ============================================================================
// HEADERS DE SEGURIDAD
// ============================================================================
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    // Permitir evento unload para Blazor Server (necesario para el funcionamiento correcto)
    context.Response.Headers.Append("Permissions-Policy", "unload=(), camera=(), microphone=(), geolocation=()");
    await next();
});

// ============================================================================
// HEALTH CHECKS ENDPOINTS
// ============================================================================
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapRazorPages();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

// ============================================================================
// LOG DE CONFIGURACIÓN APLICADA
// ============================================================================
var logger = app.Services.GetRequiredService<ILogger<Program>>();
var ollamaOptions = app.Services.GetRequiredService<IOptionsMonitor<OllamaOptions>>().CurrentValue;
logger.LogInformation(
    "GuarinoApp iniciada. Entorno: {Environment} | Ollama: {OllamaEnabled} en {OllamaUrl} | Modelo: {Modelo}",
    app.Environment.IsDevelopment() ? "Development" : "Production",
    ollamaOptions.Enabled,
    ollamaOptions.Url,
    ollamaOptions.Model);

// ============================================================================
// SEED DE BASE DE DATOS (solo Development)
// ============================================================================
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    DbInitializer.Initialize(context);
}

app.Run();

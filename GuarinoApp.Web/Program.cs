using Blazored.LocalStorage;
using GuarinoApp.Application;
using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Settings;
using GuarinoApp.Infrastructure;
using GuarinoApp.Infrastructure.Data;
using GuarinoApp.Infrastructure.Services;
using GuarinoApp.Web.Mcp;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using MudBlazor.Services;
using System.Globalization;

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
// SERVIDOR MCP (Model Context Protocol)
// Expone las skills del Asistente como tools de solo lectura en /mcp para que
// un agente externo consulte los MISMOS datos reales de la base. El endpoint
// exige el token de sesión de la app (Authorization: Bearer <token>, el mismo
// firmado con Data Protection que usa el navegador); cada tool revalida la
// matriz de módulos con el rol real del token y audita la llamada (McpCall).
// ============================================================================
builder.Services.AddScoped<GuarinoMcpTools>();
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<GuarinoMcpTools>();

// ============================================================================
// NOTIFICACIONES AL ADMINISTRADOR - captura global de errores para el reporte
// por correo. El buffer es el MISMO instance que recibe el proveedor de
// logging y que consume el notificador (hosted service registrado en
// Infrastructure). Solo observa: no altera la consola ni los demás providers.
// ============================================================================
var errorLogBuffer = new ErrorLogBuffer();
builder.Services.AddSingleton(errorLogBuffer);
builder.Logging.AddProvider(new ErrorLogBufferProvider(errorLogBuffer));

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
    // Concurrencia: varias sesiones pueden consultar la IA a la vez.
    // Ollama serializa la inferencia en el servidor; más conexiones evitan
    // que las consultas esperen en cola del lado del cliente agotando su timeout.
    MaxConnectionsPerServer = 8
});

// ============================================================================
// HEALTH CHECKS
// Monitoreo de SQL Server y Ollama para herramientas de infraestructura.
// Solo SQL Server lleva el tag "ready": es el requisito para recibir tráfico
// (sin base no hay app). Ollama es opcional (Ollama:Enabled) y su caída no
// debe marcar la instancia como no lista. Antes NINGÚN check tenía el tag y
// /health/ready respondía siempre Healthy (vacío), sin validar nada.
// ============================================================================
builder.Services.AddHealthChecks()
    .AddCheck<SqlServerHealthCheck>("sql-server", tags: new[] { "ready" })
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
// CORS
// Blazor Server atiende todo en mismo origen: las peticiones de la propia app
// (HTML, SignalR, estáticos) nunca pasan por CORS. La política por defecto
// gobierna SOLO peticiones cross-origin:
//   - Development: cualquier origen (debug en red local).
//   - Production: únicamente los orígenes listados en "Cors:AllowedOrigins"
//     (appsettings.Production.json o variable Cors__AllowedOrigins__0).
//     Lista vacía = no se emite ninguna cabecera CORS => solo mismo origen.
// ============================================================================
var corsOrigenes = (builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>())
    .Where(o => !string.IsNullOrWhiteSpace(o))
    .Select(o => o.Trim().TrimEnd('/'))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

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
            policy.WithOrigins(corsOrigenes)
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

// ============================================================================
// HEADERS DE SEGURIDAD - Debe estar al inicio del pipeline para aplicar a todas las respuestas
// ============================================================================
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    // No se restringe 'unload' porque Blazor Server lo necesita y el navegador
    // emite el warning "Permissions policy violation: unload is not allowed".
    context.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
    await next();
});

app.UseHttpsRedirection();
app.UseResponseCompression();
app.UseStaticFiles();
app.UseRouting();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// ============================================================================
// AUTENTICACIÓN DEL ENDPOINT MCP (/mcp)
// El cliente MCP envía el token de sesión de la app en «Authorization: Bearer
// <token>». El middleware lo valida y compone HttpContext.User con el rol real,
// de modo que las tools apliquen la misma matriz de módulos que la UI. Sin
// token válido responde 401.
// ============================================================================
app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/mcp"),
    branch => branch.UseMiddleware<McpAuthMiddleware>());

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

// Endpoint MCP (streamable HTTP). Debe ir ANTES del fallback para que /mcp no
// caiga en la página _Host.
app.MapMcp("/mcp");

app.MapFallbackToPage("/_Host");

// ============================================================================
// LOG DE CONFIGURACIÓN APLICADA
// ============================================================================
var logger = app.Services.GetRequiredService<ILogger<Program>>();
var ollamaOptions = app.Services.GetRequiredService<IOptionsMonitor<OllamaOptions>>().CurrentValue;
var iaWebOptions = app.Services.GetRequiredService<IOptionsMonitor<IaWebOptions>>().CurrentValue;
var iaWebEstado = !iaWebOptions.Habilitada
    ? "deshabilitada (por configuración)"
    : string.IsNullOrWhiteSpace(iaWebOptions.ApiKey)
        ? "sin clave (solo ruta local)"
        : "habilitada";
logger.LogInformation(
    "GuarinoApp iniciada. Entorno: {Environment} | Ollama: {OllamaEnabled} en {OllamaUrl} | Modelo: {Modelo} | IA web en paralelo (plan gratuito): {IaWebEstado} — {IaWebModelo}",
    app.Environment.IsDevelopment() ? "Development" : "Production",
    ollamaOptions.Enabled,
    ollamaOptions.Url,
    ollamaOptions.Model,
    iaWebEstado,
    iaWebOptions.Modelo);

// Estado CORS efectivo (visibilidad en producción: sin orígenes configurados
// la app responde solo en mismo origen).
if (app.Environment.IsDevelopment())
    logger.LogInformation("CORS: Development — cualquier origen habilitado.");
else if (corsOrigenes.Length > 0)
    logger.LogInformation("CORS: Production — solo orígenes permitidos: {Origenes}", string.Join(", ", corsOrigenes));
else
    logger.LogInformation("CORS: Production — solo mismo origen (Cors:AllowedOrigins vacío; peticiones cross-origin bloqueadas).");

// Estado de los reportes de operación (solo para el administrador).
var notifOpciones = app.Services.GetRequiredService<IOptionsMonitor<NotificacionesOptions>>().CurrentValue;
if (!notifOpciones.Habilitadas)
    logger.LogInformation("Reportes de operación al administrador: deshabilitados (Notificaciones:Habilitadas=false).");
else if (string.IsNullOrWhiteSpace(notifOpciones.EmailAdmin) || !notifOpciones.EmailAdmin.Contains('@'))
    logger.LogWarning("Reportes de operación al administrador: Notificaciones:EmailAdmin '{Email}' no es válida; no se enviarán reportes.", notifOpciones.EmailAdmin);
else
    logger.LogInformation("Reportes de operación: SOLO a {Email} cada {Minutos} min — asunto \"{Asunto}fecha_hora\".",
        notifOpciones.EmailAdmin, Math.Max(5, notifOpciones.IntervaloMinutos), notifOpciones.Asunto);

// ============================================================================
// SEED DE BASE DE DATOS (solo Development)
// Database.Migrate aplica las migraciones pendientes automáticamente en
// Development, de modo que entornos locales siempre tengan el esquema actual.
// ============================================================================
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    ApplicationDbContext? context = null;

    // Si la base no está disponible (instancia LocalDB detenida/colgada o la
    // máquina sin memoria — fallo real observado), el sanador de BD deja la
    // instancia local en condiciones (libera memoria / sqllocaldb start) y se
    // reintenta hasta 4 veces antes de rendirse, en vez de morir al primer
    // intento. Con la app arriba, el ciclo del propio sanador se encarga de los
    // reintentos y los correos de aviso.
    var sanadorBD = app.Services.GetRequiredService<BaseDatosRecuperacionService>();
    for (var intento = 1; intento <= 4; intento++)
    {
        context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        try
        {
            context.Database.Migrate();
            break;
        }
        catch (Exception ex) when (intento < 4)
        {
            logger.LogWarning(ex,
                "Migrate() falló (intento {Intento}/4); se intenta sanar la base de datos local antes de reintentar...",
                intento);
            await sanadorBD.SanearArranqueAsync(CancellationToken.None);
            await Task.Delay(TimeSpan.FromSeconds(10));
        }
    }

    DbInitializer.Initialize(context!, notifOpciones.EmailAdmin);
}

app.Run();

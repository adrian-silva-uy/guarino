using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Infrastructure.Data;
using GuarinoApp.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GuarinoApp.Infrastructure
    {
    public static class DependencyInjection
        {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
            {
            if (services == null) throw new ArgumentNullException(nameof(services));

            // Base de datos.
            // EnableRetryOnFailure reintenta automáticamente ante errores transitorios
            // de conexión (frecuentes en SQL Azure o servidores que hibernan/despiertan).
            // IMPORTANTE: este es el ÚNICO lugar donde se registra ApplicationDbContext.
            // Si se registra también en Program.cs, un registro puede pisar al otro y
            // el retry dejar de aplicarse sin que se note en el código.
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(
                    configuration.GetConnectionString("DefaultConnection"),
                    sqlServerOptions => sqlServerOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null)));

            // Contexto de aplicación
            services.AddScoped<IApplicationDbContext>(provider =>
                provider.GetRequiredService<ApplicationDbContext>());

            // Usuario actual
            services.AddScoped<ICurrentUserService, CurrentUserService>();
            services.AddHttpContextAccessor();

            // Dashboard
            services.AddScoped<IDashboardDataService, DashboardDataService>();

            // Exportación (Excel/PDF/Email)
            services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
            services.AddScoped<IExportService, ExportService>();
            services.AddScoped<IEmailService, EmailService>();

            // --- IA generativa (Ollama) con fallback a datos reales ---
            services.AddOptions<OllamaOptions>()
                .Bind(configuration.GetSection(OllamaOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddSingleton<OllamaConnectionState>();

            services.AddHttpClient("Ollama", (sp, client) =>
            {
                var options = sp.GetRequiredService<IOptionsMonitor<OllamaOptions>>().CurrentValue;
                if (!string.IsNullOrWhiteSpace(options.Url))
                    {
                    client.BaseAddress = new Uri(options.Url);
                    }
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            });

            services.AddScoped<OllamaAIService>();
            services.AddScoped<IAIService, FallbackAIService>();
            services.AddHostedService<OllamaStartupService>();

            services.AddHostedService<ClimateMonitoringHostedService>();

            // Prompts: única fuente de verdad para cargar plantillas desde
            // Resources/Prompts (usa ContentRootPath, ver PromptProvider).
            // Antes vivía registrado en Program.cs; movido acá junto con el
            // resto de los servicios de Infrastructure.
            services.AddSingleton<IPromptProvider, PromptProvider>();

            // Registrar generador de PDFs con QuestPDF
            services.AddScoped<IPdfGenerator, QuestPdfGenerator>();

            return services;
            }
        }
    }

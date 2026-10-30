using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Settings;
using GuarinoApp.Infrastructure.Data;
using GuarinoApp.Infrastructure.Services;
using GuarinoApp.Infrastructure.Services.Auth;
using GuarinoApp.Infrastructure.Services.Skills;
using Microsoft.AspNetCore.Components.Authorization;
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

            // Destinatarios de los reportes pedidos desde la interfaz:
            // usuario logueado con copia al administrador (Notificaciones:EmailAdmin).
            services.AddScoped<IReportMailRouter, ReportMailRouter>();

            // Reporte de operación SOLO para el administrador (único destinatario:
            // Notificaciones:EmailAdmin). Cada N minutos envía errores del log,
            // salud de la IA (servicio/modelo/clave) y actualizaciones pendientes
            // con asunto "Guarino App - logs del día : fecha_hora".
            services.Configure<NotificacionesOptions>(configuration.GetSection(NotificacionesOptions.SectionName));
            services.AddHostedService<NotificadorAdminService>();

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

            // Auto-reparación silenciosa de la IA local: si la generación falla con
            // 5xx (proceso "colgado") o el proceso deja de responder, lo reinicia,
            // verifica con una generación de prueba y avisa por correo a
            // Notificaciones:EmailAdmin con lo ocurrido y la solución. Mientras
            // tanto los usuarios quedan cubiertos por la IA web / planes
            // predefinidos. Configuración: Ollama:Sanacion.
            services.AddHostedService<OllamaRecuperacionService>();

            // Auto-reparación silenciosa de la BASE DE DATOS: verifica la
            // conexión cada BaseDatos:Sanacion:IntervaloSegundos y, solo si el
            // destino es local, libera memoria (modelos IA residentes), levanta
            // la instancia por instancia (sqllocaldb / sc) y avisa por correo UNA
            // vez por incidente a Notificaciones:EmailAdmin. Mismo contrato que
            // la sanación de Ollama. Configuración: BaseDatos:Sanacion.
            // Singleton + IHostedService para que Program.cs pueda llamar
            // SanearArranqueAsync en el arranque de Development.
            services.AddOptions<BaseDatosOptions>()
                .Bind(configuration.GetSection(BaseDatosOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();
            services.AddSingleton<BaseDatosRecuperacionService>();
            services.AddHostedService(sp => sp.GetRequiredService<BaseDatosRecuperacionService>());

            // Caché de resultados IA: en picos (~20 usuarios) la misma consulta
            // repetida por varias cargas de página se contesta una sola vez y el
            // resto sale del caché al instante (ahorra cupo del plan gratuito y
            // turnos de la compuerta local). Singleton: compartida por circuitos.
            services.AddSingleton<AiResultCache>();

            // --- Servicio web de IA en PARALELO a la ruta local (plan gratuito) ---
            // La consulta armada sale simultáneamente a Ollama y al servicio web;
            // la ruta local tiene prioridad y la respuesta web solo se consume
            // cuando la local no responde. Sin clave configurada, Habilitada=false
            // y todo sigue igual que antes (local + cálculo determinista).
            // La clave NUNCA va al repo: user-secrets (dev) o IaWeb__ApiKey (prod).
            services.AddOptions<IaWebOptions>()
                .Bind(configuration.GetSection(IaWebOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddHttpClient("IaWeb", (sp, client) =>
            {
                var options = sp.GetRequiredService<IOptionsMonitor<IaWebOptions>>().CurrentValue;
                client.BaseAddress = new Uri(options.UrlBase.TrimEnd('/') + "/");
                client.Timeout = TimeSpan.FromSeconds(Math.Max(options.TimeoutSegundos, 5));
            });

            // Singleton: el candado de consultas simultáneas del cupo gratuito
            // debe ser único para toda la aplicación (no por circuito).
            services.AddSingleton<IIaWebClient, IaWebClient>();

            // --- MCP: cliente de servidores externos (SPEC 003) ---
            // Solo lectura, allowlist por configuración y orquestación
            // determinista (ADR-003). Cada llamada saliente queda auditada.
            services.AddOptions<McpOptions>()
                .Bind(configuration.GetSection(McpOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();
            services.AddScoped<IMcpDataService, McpDataService>();

            services.AddHostedService<ClimateMonitoringHostedService>();

            // Prompts: única fuente de verdad para cargar plantillas desde
            // Resources/Prompts (usa ContentRootPath, ver PromptProvider).
            // Antes vivía registrado en Program.cs; movido acá junto con el
            // resto de los servicios de Infrastructure.
            services.AddSingleton<IPromptProvider, PromptProvider>();

            // Registrar generador de PDFs con QuestPDF
            services.AddScoped<IPdfGenerator, QuestPdfGenerator>();

            // Registrar servicio de auditoría
            services.AddScoped<IAuditService, AuditService>();

            // Skills consultables por la IA: consultas reales de negocio que se
            // inyectan como contexto en el prompt (enrutamiento por intención).
            services.AddScoped<ISkill, FinancialDataSkill>();
            services.AddScoped<ISkill, ProyeccionZafraSkill>();
            services.AddScoped<ISkill, ResumenLiquidacionesSkill>();
            services.AddScoped<ISkill, ActividadAuditoriaSkill>();
            // Ante empate de keywords gana la registrada primero y esta skill
            // (que además escribe en BD) no debe robarle intenciones a las de
            // consulta: por eso las skills de consulta operativa van después.
            services.AddScoped<ISkill, ContactosImportSkill>();
            // Skills de consulta operativa diaria: stock por variedad, pesadas,
            // volcados/lotes, contactos, pallets sin cliente, indicadores de
            // gerencia y clima — respuestas del chat con datos reales de la
            // planta (las de consulta jamás escriben en BD).
            services.AddScoped<ISkill, StockPlantaSkill>();
            services.AddScoped<ISkill, PesadasSkill>();
            services.AddScoped<ISkill, VolcadosLotesSkill>();
            services.AddScoped<ISkill, ContactosResumenSkill>();
            services.AddScoped<ISkill, PalletsSkill>();
            services.AddScoped<ISkill, IndicadoresGerenciaSkill>();
            services.AddScoped<ISkill, ClimaRiesgoSkill>();
            // Nuevas skills financieras (Contaduría / Gerencia) — puntos 1 al 4 del análisis de Contador Senior
            services.AddScoped<ISkill, CostoDesgloseSkill>();
            services.AddScoped<ISkill, RendimientoPackingSkill>();
            services.AddScoped<ISkill, ComparativaZafraSkill>();
            services.AddScoped<ISkill, ProyeccionCajaSkill>();
            services.AddScoped<ISkillRegistry, SkillRegistry>();

            // Filtro IA por rol (capa 1): clasifica la consulta por temas según
            // la matriz de módulos y rechaza con mensaje inteligente + auditoría
            // cuando el rol no puede consultar ese tema. Se invoca en el único
            // punto de entrada del chat, antes de la caché.
            services.AddScoped<IFiltroConsultasIA, FiltroConsultasIA>();

            // Registrar servicio de autorización
            services.AddScoped<IAuthorizationService, AuthorizationService>();

            // Configuración de identidad y roles (respaldo cuando Windows Auth
            // no entrega identidad, p. ej. durante la vida del circuito).
            services.Configure<SecuritySettings>(configuration.GetSection(SecuritySettings.SectionName));

            // Configuración del modo de autenticación (Local con login / Windows con AD).
            services.Configure<AuthSettings>(configuration.GetSection(AuthSettings.SectionName));

            // Protección de datos para los tokens de sesión (claves del servidor).
            services.AddDataProtection();

            // Sesión del circuito: fuente de verdad del usuario autenticado.
            services.AddScoped<ICurrentUserState, CurrentUserState>();
            services.AddScoped<IAuthTokenService, AuthTokenService>();
            services.AddScoped<IPasswordHasher, PasswordHasher>();

            // Registrar proveedor de estado de autenticación personalizado
            // Debe ser Scoped porque ICurrentUserService es Scoped
            services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();
            services.AddScoped<IAuthenticationStateNotifier>(sp =>
                (IAuthenticationStateNotifier)sp.GetRequiredService<AuthenticationStateProvider>());

            return services;
            }
        }
    }

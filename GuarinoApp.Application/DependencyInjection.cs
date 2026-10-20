using FluentValidation;
using GuarinoApp.Application.Common.Behaviors;
using GuarinoApp.Application.Modules.Auth.Services;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace GuarinoApp.Application;

public static class DependencyInjection
    {
    public static IServiceCollection AddApplication(this IServiceCollection services)
        {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        // Limpieza automática y central de todo texto de entrada (OWASP
        // Input Validation & Sanitization) antes de llegar a los handlers.
        services.AddTransient(
            typeof(IPipelineBehavior<,>),
            typeof(InputSanitizationBehavior<,>));

        // Serialización por circuito de los requests MediatR: el DbContext es
        // escopado por circuito y dos cargas simultáneas provocaban
        // "A second operation was started on this context instance"
        // (Auditoría en 0 registros durante la prueba de fuego). Scoped = una
        // puerta por circuito; entre usuarios no hay bloqueo.
        services.AddScoped<PuertaSerializacionDbContext>();
        services.AddTransient(
            typeof(IPipelineBehavior<,>),
            typeof(SerializacionDbContextBehavior<,>));

        // Bloqueo temporal de cuentas tras intentos fallidos de login (fuerza
        // bruta): singleton para que el contador se comparta entre circuitos.
        services.AddSingleton<ControlIntentosLogin>();

        return services;
        }
    }

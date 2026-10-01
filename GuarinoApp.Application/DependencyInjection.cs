using FluentValidation;
using GuarinoApp.Application.Common.Behaviors;
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

        return services;
        }
    }

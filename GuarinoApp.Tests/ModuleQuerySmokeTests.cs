using GuarinoApp.Application;
using GuarinoApp.Infrastructure;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GuarinoApp.Tests;

[TestClass]
public sealed class ModuleQuerySmokeTests
{
    [TestMethod]
    public async Task EveryModuleQuery_IsRegisteredAndCompletesAgainstAnEmptyIsolatedDatabase()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Testing",
                ["Auth:Mode"] = "Local",
                ["Ollama:Enabled"] = "false",
                ["IaWeb:Habilitada"] = "false",
                ["Notificaciones:Habilitadas"] = "false"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var queryTypes = typeof(GuarinoApp.Application.DependencyInjection).Assembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && type.Namespace?.Contains(".Modules.", StringComparison.Ordinal) == true)
            .Where(type => type.GetInterfaces().Any(contract =>
                contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IRequest<>)))
            .Where(type => type.Namespace!.Contains(".Queries.", StringComparison.Ordinal))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        Assert.IsTrue(queryTypes.Length >= 25, $"Se descubrieron sólo {queryTypes.Length} consultas de módulos.");
        var failures = new List<string>();
        foreach (var queryType in queryTypes)
        {
            try
            {
                var constructor = queryType.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
                var arguments = constructor.GetParameters().Select(parameter =>
                {
                    if (parameter.HasDefaultValue)
                        return parameter.DefaultValue;
                    return parameter.ParameterType.IsValueType
                        ? Activator.CreateInstance(parameter.ParameterType)
                        : null;
                }).ToArray();
                var request = constructor.Invoke(arguments);
                await mediator.Send(request, CancellationToken.None);
            }
            catch (Exception exception)
            {
                failures.Add($"{queryType.FullName}: {exception.GetBaseException().Message}");
            }
        }

        Assert.AreEqual(0, failures.Count,
            $"Consultas de módulos que no resolvieron contra una BD vacía:{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
    }
}

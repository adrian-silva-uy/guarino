using System.Security.Claims;
using GuarinoApp.Application.Common.Helpers;
using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.Contaduria.Commands.UpdateFinancialData;
using GuarinoApp.Application.Modules.Contaduria.Queries.GetFinancialData;
using GuarinoApp.Application.Modules.Contaduria.Queries.GetProyeccionCaja;
using GuarinoApp.Application.Modules.Proyeccion.Dtos;
using GuarinoApp.Application.Modules.Proyeccion.Queries.GetProyeccionZafra;
using GuarinoApp.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GuarinoApp.Tests;

[TestClass]
public sealed class FinancialWorkflowTests
{
    [TestMethod]
    public async Task Contaduria_IngresoFinanciero_AlimentaKpisEscenariosYProyeccionSemanalDeCaja()
    {
        var databaseName = $"FinancialWorkflow-{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(databaseName).Options;
        var audit = new RecordingAuditService();
        var user = new TestCurrentUserService();
        await using var db = new ApplicationDbContext(options);

        var data = await new UpdateFinancialDataCommandHandler(db, user, audit).Handle(
            new UpdateFinancialDataCommand(
                CostoPorKg: 1.50m,
                FlujoCaja: 100_000m,
                MargenBruto: 20m,
                CostoFlete: 2_500m,
                CapacidadInstalada: 900_000m,
                Observaciones: "Presupuesto de prueba"),
            CancellationToken.None);

        Assert.AreEqual(1.50m, data.CostoPorKg);
        Assert.AreEqual(100_000m, data.FlujoCaja);
        Assert.AreEqual("contador-demo", data.ActualizadoPor);
        Assert.AreEqual("Create", audit.Actions.Single());

        var reloaded = await new GetFinancialDataQueryHandler(db).Handle(new GetFinancialDataQuery(), CancellationToken.None);
        Assert.IsNotNull(reloaded);
        Assert.AreEqual(data.Id, reloaded.Id);

        const decimal projectedVolumeKg = 1_300_000m;
        var kpi = FinancialKpiCalculator.Calcular(projectedVolumeKg, 850_000m, reloaded);
        Assert.AreEqual(1_950_000m, kpi.CapitalNecesario);
        Assert.AreEqual(1_850_000m, kpi.BrechaLiquidez);
        Assert.AreEqual("ALTO", kpi.RiesgoLiquidez);
        Assert.AreEqual("ALTA", kpi.OportunidadCAPEX);

        var scenarios = FinancialKpiCalculator.ConstruirEscenarios(projectedVolumeKg, reloaded);
        Assert.AreEqual(3, scenarios.Count);
        Assert.AreEqual("Base", scenarios[0].Nombre);
        Assert.AreEqual(1_300_000m, scenarios[0].Volumen);
        Assert.AreEqual("Pesimista (-20%)", scenarios[1].Nombre);
        Assert.AreEqual(1_040_000m, scenarios[1].Volumen);
        Assert.AreEqual("Optimista (+20%)", scenarios[2].Nombre);
        Assert.AreEqual(1_560_000m, scenarios[2].Volumen);
        StringAssert.Contains(scenarios[0].ImpactoCaja, "Faltan");

        // La proyección semanal consume los mismos datos guardados por Contaduría.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(GetProyeccionCajaQueryHandler).Assembly));
        services.AddDbContext<ApplicationDbContext>(builder => builder.UseInMemoryDatabase(databaseName));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddTransient<IRequestHandler<GetProyeccionZafraQuery, List<ZafraDto>>>(_ =>
            new FixedZafraQueryHandler(projectedVolumeKg));

        await using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();
        var projectionHandler = new GetProyeccionCajaQueryHandler(db, mediator);

        var basePlan = await projectionHandler.Handle(new GetProyeccionCajaQuery("Base"), CancellationToken.None);
        var pessimisticPlan = await projectionHandler.Handle(new GetProyeccionCajaQuery("Pesimista"), CancellationToken.None);
        var optimisticPlan = await projectionHandler.Handle(new GetProyeccionCajaQuery("Optimista"), CancellationToken.None);

        Assert.AreEqual(100_000m, basePlan.SaldoInicialUsd);
        Assert.AreEqual(13, basePlan.Datos.Count);
        Assert.AreEqual(1, basePlan.Datos[0].Semana);
        Assert.AreEqual("Base", basePlan.Datos[0].Escenario);
        Assert.AreEqual(150_000m, basePlan.Datos[0].EntradasUsd);
        Assert.AreEqual(228_500m, basePlan.Datos[0].SaldoAcumuladoUsd);
        Assert.AreEqual(120_000m, pessimisticPlan.Datos[0].EntradasUsd);
        Assert.AreEqual(202_800m, pessimisticPlan.Datos[0].SaldoAcumuladoUsd);
        Assert.AreEqual(180_000m, optimisticPlan.Datos[0].EntradasUsd);
        Assert.AreEqual(254_200m, optimisticPlan.Datos[0].SaldoAcumuladoUsd);
        Assert.IsTrue(basePlan.Datos.All(row => !row.EsReal));
    }

    private sealed class FixedZafraQueryHandler(decimal volume) : IRequestHandler<GetProyeccionZafraQuery, List<ZafraDto>>
    {
        public Task<List<ZafraDto>> Handle(GetProyeccionZafraQuery request, CancellationToken cancellationToken) =>
            Task.FromResult(new List<ZafraDto>
            {
                new() { Año = DateTime.Today.Year + 1, TotalKgs = volume, Estado = "Proyectado", EsDummy = false }
            });
    }

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public string? UserId => "contador-demo-id";
        public string? UserName => "contador-demo";
        public bool IsAuthenticated => true;
        public bool IsInRole(string role) => role == "Contador";
        public IEnumerable<Claim> Claims => new[] { new Claim(ClaimTypes.Role, "Contador") };
    }

    private sealed class RecordingAuditService : IAuditService
    {
        public List<string> Actions { get; } = [];
        public Task LogAsync(string action, string module, string description, string? additionalData = null)
        {
            Actions.Add(action);
            return Task.CompletedTask;
        }
    }
}

using GuarinoApp.Application.Common;
using GuarinoApp.Application.Common.Helpers;
using GuarinoApp.Application.Modules.Contaduria.Dtos;
using GuarinoApp.Infrastructure.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GuarinoApp.Tests;

[TestClass]
public sealed class CoreRulesTests
{
    [TestMethod]
    public void RoleNames_DefineExpectedAccessMatrix()
    {
        CollectionAssert.AreEquivalent(new[] { "Admin", "Gerencia", "Contador", "Operador" }, RoleNames.Todos.ToArray());
        CollectionAssert.AreEquivalent(new[] { "Admin", "Gerencia", "Contador" }, RoleNames.Financieros.ToArray());
        CollectionAssert.AreEquivalent(new[] { "Admin", "Gerencia" }, RoleNames.Administracion.ToArray());
    }

    [TestMethod]
    public void SanitizadorTexto_UnaLinea_RemovesControlsTagsAndCollapsesSpaces()
    {
        var result = SanitizadorTexto.UnaLinea("  Ana\t <b>Silva</b>\r\n\0 ");

        Assert.AreEqual("Ana Silva", result);
    }

    [TestMethod]
    public void SanitizadorTexto_Multilinea_PreservesMeaningfulNewlinesAndTruncates()
    {
        var result = SanitizadorTexto.Multilinea(" línea 1\n\n línea 2 ", 100);

        Assert.AreEqual("línea 1\n\n línea 2", result);
    }

    [TestMethod]
    public void InputSanitizer_RemovesSqlStructuralTokensAndIsIdempotent()
    {
        const string input = "O'Brien; DROP TABLE X -- comentario /* bloque */ `id`";

        var sanitized = GuarinoApp.Application.Common.Security.InputSanitizer.Sanitize(input);

        Assert.IsNotNull(sanitized);
        Assert.IsFalse(GuarinoApp.Application.Common.Security.InputSanitizer.ContainsSqlInjectionPattern(sanitized));
        Assert.AreEqual(sanitized, GuarinoApp.Application.Common.Security.InputSanitizer.Sanitize(sanitized));
        StringAssert.Contains(sanitized!, "O'Brien");
    }

    [TestMethod]
    public void TextoIA_NormalizaAcentosYCuentaPalabrasCompletas()
    {
        Assert.AreEqual("proyeccion de zafra", TextoIA.Normalizar("Proyección de Zafra"));
        Assert.AreEqual(1, TextoIA.ContarCoincidencias("cajas de fruta", new[] { "caja", "fruta" }));
    }

    [TestMethod]
    public void AiResultCache_GetsSameTypedValueAndRejectsWrongType()
    {
        var cache = new AiResultCache();
        var key = AiResultCache.Clave("dashboard", 2026, "base");
        cache.Set(key, "respuesta");

        Assert.IsTrue(cache.TryGet<string>(key, out var value));
        Assert.AreEqual("respuesta", value);
        Assert.IsFalse(cache.TryGet<int>(key, out _));
        Assert.AreNotEqual(key, AiResultCache.Clave("dashboard", 2027, "base"));
    }

    [TestMethod]
    public void FinancialKpiCalculator_CalculatesLiquidityRiskAndScenarios()
    {
        var data = new FinancialDataDto
        {
            CostoPorKg = 2m,
            FlujoCaja = 100m,
            CapacidadInstalada = 1_000m
        };

        var kpi = FinancialKpiCalculator.Calcular(100m, 120m, data);
        var scenarios = FinancialKpiCalculator.ConstruirEscenarios(100m, data);

        Assert.AreEqual(200m, kpi.CapitalNecesario);
        Assert.AreEqual(100m, kpi.BrechaLiquidez);
        Assert.AreEqual("ALTO", kpi.RiesgoLiquidez);
        Assert.AreEqual(3, scenarios.Count);
        Assert.AreEqual("Base", scenarios[0].Nombre);
    }
}

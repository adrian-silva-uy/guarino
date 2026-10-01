using GuarinoApp.Application.Modules.Contaduria.Dtos;
using GuarinoApp.Application.Modules.Proyeccion.Dtos;

namespace GuarinoApp.Application.Common.Helpers;

/// <summary>
/// Fuente única de verdad de los KPIs financieros y escenarios de la proyección
/// de zafra. Lo usan la página ProyeccionZafra (cálculo local con datos de
/// Contaduría) y el servicio de respaldo del asistente, para que los números
/// sean idénticos venga la IA o no.
/// </summary>
public static class FinancialKpiCalculator
    {
    /// <summary>
    /// Capital a financiar = costo directo de la zafra proyectada (kg × costo/kg).
    /// El stock en bins NO se resta: está expresado en unidades, no en la misma
    /// moneda que el capital — restarlo mezclaría unidades y daría un número falso.
    /// Sin datos de Contaduría devuelve N/D, pero la oportunidad de CAPEX se evalúa
    /// igual contra la capacidad (no depende de costos).
    /// </summary>
    public static FinancialKpiDto Calcular(
        decimal volumenProyectadoKgs,
        decimal capacidadMaxHistoricaKgs,
        FinancialDataDto? datos)
        {
        var capex = CalcularOportunidadCapex(volumenProyectadoKgs, capacidadMaxHistoricaKgs, datos);

        if (datos == null)
            {
            return new FinancialKpiDto
                {
                CapitalNecesario = 0m,
                BrechaLiquidez = 0m,
                RiesgoLiquidez = "N/D",
                OportunidadCAPEX = capex
                };
            }

        var capital = Math.Max(0m, Math.Round(volumenProyectadoKgs * datos.CostoPorKg, 0));
        var brecha = capital - datos.FlujoCaja;

        var riesgo = "N/D";
        if (datos.FlujoCaja > 0)
            {
            var porcentajeBrecha = brecha / datos.FlujoCaja * 100;
            riesgo = porcentajeBrecha > 30 ? "ALTO" : porcentajeBrecha > 10 ? "MEDIO" : "BAJO";
            }

        return new FinancialKpiDto
            {
            CapitalNecesario = capital,
            BrechaLiquidez = brecha,
            RiesgoLiquidez = riesgo,
            OportunidadCAPEX = capex
            };
        }

    /// <summary>
    /// Escenarios Base / Pesimista (-20%) / Optimista (+20%). Con datos de
    /// Contaduría cada escenario muestra la brecha real de caja en USD; sin
    /// ellos, el impacto queda en "—" (no se inventan cifras).
    /// </summary>
    public static List<ScenarioDto> ConstruirEscenarios(decimal volumenBaseKgs, FinancialDataDto? datos)
        {
        ScenarioDto Crear(string nombre, decimal factor, string riesgoPorDefecto, bool esBase)
            {
            var volumen = Math.Round(volumenBaseKgs * factor, 0);
            var riesgo = riesgoPorDefecto;
            var impacto = "—";

            if (datos != null)
                {
                var capital = Math.Round(volumen * datos.CostoPorKg, 0);
                var brecha = capital - datos.FlujoCaja;
                impacto = brecha > 0
                    ? $"Faltan ${brecha:N0}"
                    : $"Alcanza la caja (sobra ${-brecha:N0})";

                if (esBase && datos.FlujoCaja > 0)
                    {
                    var porcentajeBrecha = brecha / datos.FlujoCaja * 100;
                    riesgo = porcentajeBrecha > 30 ? "Alto" : porcentajeBrecha > 10 ? "Medio" : "Bajo";
                    }
                }

            return new ScenarioDto
                {
                Nombre = nombre,
                Volumen = volumen,
                ImpactoCaja = impacto,
                Riesgo = riesgo
                };
            }

        return new List<ScenarioDto>
            {
            Crear("Base", 1m, "Bajo", esBase: true),
            Crear("Pesimista (-20%)", 0.8m, "Alto", esBase: false),
            Crear("Optimista (+20%)", 1.2m, "Medio", esBase: false)
            };
        }

    /// <summary>
    /// Benchmark construido siempre con datos VERIFICADOS de la propia empresa:
    /// variación interanual, mejor zafra registrada y capacidad instalada. Si no
    /// hay datos de Contaduría se explica qué falta con tono de sugerencia
    /// opcional — nunca como bloqueo ni con nombres de herramientas internas.
    /// </summary>
    public static string ConstruirComparativaBenchmark(
        ZafraDto proyectada,
        decimal capacidadMaxHistoricaKgs,
        FinancialDataDto? datos)
        {
        var partes = new List<string>();

        var variacion = proyectada.VariacionPorcentual;
        decimal? volumenAnterior = null;
        if (variacion > -100m && variacion != 0m)
            volumenAnterior = Math.Round(proyectada.TotalKgs / (1 + variacion / 100m), 0);

        var frase = $"La proyección de {proyectada.TotalKgs:N0} kg para {proyectada.Año}";
        frase += volumenAnterior.HasValue
            ? $" implica {(variacion >= 0 ? "+" : "")}{variacion:0.0}% sobre la zafra {proyectada.Año - 1} ({volumenAnterior.Value:N0} kg)."
            : " mantiene el volumen registrado en la última zafra.";
        partes.Add(frase);

        if (capacidadMaxHistoricaKgs > 0)
            {
            var pctMejor = proyectada.TotalKgs / capacidadMaxHistoricaKgs * 100;
            partes.Add(pctMejor > 100
                ? $"Supera la mejor zafra registrada ({capacidadMaxHistoricaKgs:N0} kg) en un {(pctMejor - 100):0.0}%."
                : $"Equivale al {pctMejor:0.0}% de la mejor zafra registrada ({capacidadMaxHistoricaKgs:N0} kg).");
            }

        if (datos != null && datos.CapacidadInstalada > 0)
            {
            var pctCapacidad = proyectada.TotalKgs / datos.CapacidadInstalada * 100;
            partes.Add(pctCapacidad > 100
                ? $"Requiere {(pctCapacidad - 100):0.0}% por encima de la capacidad instalada ({datos.CapacidadInstalada:N0} kg): planificar turno extra o tercerización."
                : $"Representa el {pctCapacidad:0.0}% de la capacidad instalada ({datos.CapacidadInstalada:N0} kg): sin necesidad de CAPEX por capacidad.");
            }

        partes.Add(datos != null
            ? $"Capital y brecha calculados con los datos vigentes de Contaduría (costo ${datos.CostoPorKg:0.00}/kg, caja de ${datos.FlujoCaja:N0})."
            : "Si desea calcular capital y brecha, complete el costo por kg y el flujo de caja en el módulo de Contaduría (opcional, toma un minuto).");

        return string.Join(" ", partes);
        }

    private static string CalcularOportunidadCapex(
        decimal volumenProyectadoKgs,
        decimal capacidadMaxHistoricaKgs,
        FinancialDataDto? datos)
        {
        var capacidad = datos != null && datos.CapacidadInstalada > 0
            ? datos.CapacidadInstalada
            : capacidadMaxHistoricaKgs;

        return capacidad > 0 && volumenProyectadoKgs > capacidad * 1.15m ? "ALTA" : "BAJA";
        }
    }

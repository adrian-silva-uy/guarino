using System.Text.Json;
using GuarinoApp.Application.Common.Dtos;
using GuarinoApp.Application.Modules.Contaduria.Dtos;
using GuarinoApp.Application.Modules.Proyeccion.Dtos;

namespace GuarinoApp.Application.Modules.Contaduria.Services;

/// <summary>
/// Análisis determinista del módulo Contaduría. Se construye SIEMPRE con las cifras
/// vigentes (FinancialData + KPIs de la proyección) para que la página tenga lectura,
/// hallazgos y acciones de inmediato; la IA refina el texto en segundo plano y solo
/// se adopta si el parseo es correcto (EsIA=true). Ningún número sale de la fuente:
/// no se inventan valores de industria.
/// </summary>
public static class AnalisisContaduriaBuilder
{
    public static ModuloInsightDto Construir(
        FinancialDataDto? datos,
        FinancialKpiDto? kpi,
        decimal volumenProyectadoKgs)
        {
        var insight = new ModuloInsightDto();

        if (datos == null)
            {
            insight.Lectura =
                "Todavía no hay cifras de Contaduría cargadas (costo por kg, caja, margen). " +
                "Completá el formulario de arriba para activar el análisis financiero y el cálculo " +
                "de capital de trabajo de la próxima zafra.";
            insight.Hallazgos.Add(new InsightHallazgoDto
                {
                Titulo = "Sin datos financieros",
                Detalle = "Los tableros de proyección usan estas cifras como fuente única de costo, caja y margen.",
                Severidad = "media"
                });
            insight.Acciones.Add(new InsightAccionDto
                {
                Texto = "Cargar costo por kg, flujo de caja y margen bruto vigentes",
                Plazo = "inmediato",
                Impacto = "activa KPIs de capital de trabajo y riesgo de liquidez"
                });
            return insight;
            }

        var caja = datos.FlujoCaja;
        var capital = kpi?.CapitalNecesario ?? 0m;
        var brecha = kpi?.BrechaLiquidez ?? 0m;
        var riesgo = string.IsNullOrWhiteSpace(kpi?.RiesgoLiquidez) ? "N/D" : kpi!.RiesgoLiquidez;
        var capacidad = datos.CapacidadInstalada;
        var ocupacionPct = capacidad > 0 ? volumenProyectadoKgs / capacidad * 100 : 0m;
        var margen = datos.MargenBruto;
        var costo = datos.CostoPorKg;
        var flete = datos.CostoFlete;
        var diasDesdeActualizacion = (DateTime.Today - datos.FechaActualizacion.Date).Days;

        insight.Lectura =
            $"Caja disponible ${caja:N0} frente a un capital de trabajo de ${capital:N0} requerido por " +
            $"la zafra proyectada ({volumenProyectadoKgs:N0} kg): la brecha es ${brecha:N0} y el riesgo de " +
            $"liquidez es {riesgo}. Costo operativo ${costo:N2}/kg con margen bruto {margen:N1}%" +
            (flete is > 0 ? $" y flete de ${flete:N0} por contenedor" : string.Empty) +
            (capacidad > 0
                ? $". La planta usaría el {ocupacionPct:N1}% de su capacidad instalada de {capacidad:N0} kg."
                : ".");

        // --- Hallazgos (reglas sobre datos reales, máx. 4) ---
        if (brecha > 0)
            {
            insight.Hallazgos.Add(new InsightHallazgoDto
                {
                Titulo = "Brecha de capital de trabajo",
                Detalle = $"Faltan ${brecha:N0} para cubrir el capital requerido (${capital:N0}) con la caja actual de ${caja:N0}. Riesgo de liquidez {riesgo}.",
                Severidad = riesgo == "ALTO" ? "alta" : riesgo == "MEDIO" ? "media" : "baja"
                });
            }
        else if (caja >= capital && capital > 0)
            {
            insight.Hallazgos.Add(new InsightHallazgoDto
                {
                Titulo = "Caja holgada frente al capital requerido",
                Detalle = $"La caja de ${caja:N0} cubre el capital de ${capital:N0} con ${caja - capital:N0} de margen.",
                Severidad = "baja"
                });
            }

        if (capacidad > 0)
            {
            if (ocupacionPct >= 90)
                {
                insight.Hallazgos.Add(new InsightHallazgoDto
                    {
                    Titulo = "Capacidad instalada al tope",
                    Detalle = $"La proyección de {volumenProyectadoKgs:N0} kg equivale al {ocupacionPct:N1}% de los {capacidad:N0} kg instalados: cualquier pico obliga a terceros.",
                    Severidad = "alta"
                    });
                }
            else
                {
                insight.Hallazgos.Add(new InsightHallazgoDto
                    {
                    Titulo = "Ocupación de capacidad",
                    Detalle = $"Al {ocupacionPct:N1}% de los {capacidad:N0} kg instalados queda holgura de {(capacidad - volumenProyectadoKgs):N0} kg.",
                    Severidad = "baja"
                    });
                }
            }

        if (margen < 30)
            {
            insight.Hallazgos.Add(new InsightHallazgoDto
                {
                Titulo = "Margen bruto presionado",
                Detalle = $"Margen de {margen:N1}% por debajo del 30% con costo ${costo:N2}/kg: revisar estructura de mano de obra y flete.",
                Severidad = "media"
                });
            }
        else
            {
            insight.Hallazgos.Add(new InsightHallazgoDto
                {
                Titulo = "Margen bruto sano",
                Detalle = $"Margen de {margen:N1}% sobre costo operativo de ${costo:N2}/kg.",
                Severidad = "baja"
                });
            }

        if (diasDesdeActualizacion > 90)
            {
            insight.Hallazgos.Add(new InsightHallazgoDto
                {
                Titulo = "Cifras desactualizadas",
                Detalle = $"Última actualización {datos.FechaActualizacion:dd/MM/yyyy} ({diasDesdeActualizacion} días): refrescar costo y caja para que los tableros reflejen la realidad.",
                Severidad = "media"
                });
            }

        // --- Acciones (máx. 4) ---
        if (brecha > 0)
            {
            insight.Acciones.Add(new InsightAccionDto
                {
                Texto = $"Cubrir la brecha de ${brecha:N0} con anticipo de exportación o línea de crédito antes del inicio de cosecha",
                Plazo = "antes de la zafra",
                Impacto = "evita cuellos de caja durante la temporada"
                });
            }

        if (capacidad > 0 && ocupacionPct >= 90)
            {
            insight.Acciones.Add(new InsightAccionDto
                {
                Texto = $"Reservar empaque y frío de terceros para el exceso sobre {capacidad:N0} kg",
                Plazo = "esta semana",
                Impacto = "sin pérdida de volumen exportable"
                });
            }

        if (flete is > 0)
            {
            insight.Acciones.Add(new InsightAccionDto
                {
                Texto = $"Anticipar la reserva de contenedores (${flete:N0}/contenedor) según el calendario de volcados",
                Plazo = "30 días antes",
                Impacto = "mejores cupos y tarifas en la ventana de exportación"
                });
            }

        insight.Acciones.Add(new InsightAccionDto
            {
            Texto = "Actualizar costo por kg, caja y margen con el cierre del mes en Contaduría",
            Plazo = "mensual",
            Impacto = "KPIs y tableros siempre al día"
            });

        return insight;
        }

    /// <summary>
    /// Contexto JSON que se le entrega al motor de IA: exactamente las mismas cifras
    /// que la página muestra en pantalla, para que el análisis refinado no se contradiga
    /// con los números del tablero.
    /// </summary>
    public static string ConstruirContextoJson(
        FinancialDataDto? datos,
        FinancialKpiDto? kpi,
        decimal volumenProyectadoKgs)
        {
        var contexto = new
            {
            fechaConsulta = DateTime.Today.ToString("yyyy-MM-dd"),
            empresa = "Guarino (NORIDEL SA), Salto, Uruguay — cítricos frescos para exportación y mercado interno",
            volumenProyectadoKgs,
            datosFinancieros = datos == null ? null : new
                {
                costoPorKg = datos.CostoPorKg,
                flujoCaja = datos.FlujoCaja,
                margenBrutoPct = datos.MargenBruto,
                costoFleteContenedor = datos.CostoFlete,
                capacidadInstaladaKg = datos.CapacidadInstalada,
                fechaActualizacion = datos.FechaActualizacion.ToString("yyyy-MM-dd"),
                observaciones = datos.Observaciones
                },
            kpis = kpi == null ? null : new
                {
                capitalNecesario = kpi.CapitalNecesario,
                brechaLiquidez = kpi.BrechaLiquidez,
                riesgoLiquidez = kpi.RiesgoLiquidez,
                // Va precalculado para que el análisis lo COPIE y no lo recalcule:
                // el modelo a veces mezcla margen con capacidad y da un % equivocado.
                ocupacionCapacidadPct = datos != null && datos.CapacidadInstalada > 0
                    ? Math.Round(volumenProyectadoKgs / datos.CapacidadInstalada * 100, 1)
                    : 0
                }
            };

        return JsonSerializer.Serialize(contexto, new JsonSerializerOptions
            {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
}

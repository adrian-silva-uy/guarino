using System.Text.Json;
using GuarinoApp.Application.Common.Dtos;
using GuarinoApp.Application.Modules.Gerencia.Dtos;

namespace GuarinoApp.Application.Modules.Gerencia.Services;

/// <summary>
/// Análisis determinista del Panel de Gerencia: lee los indicadores ya calculados por
/// el handler y arma lectura, hallazgos y acciones con esos números exactos. La página
/// lo muestra de inmediato y la IA refina el texto en segundo plano (solo se adopta si
/// el parseo es correcto, EsIA=true). Si falta un indicador simplemente no se comenta.
/// </summary>
public static class AnalisisGerenciaBuilder
{
    public static ModuloInsightDto Construir(
        IReadOnlyList<IndicadorGerenciaDto> indicadores,
        decimal volumenProyectadoKgs = 0,
        decimal capacidadInstaladaKgs = 0,
        decimal variacionProyectadaPct = 0)
        {
        var insight = new ModuloInsightDto();

        var volcadosTotal = Valor(indicadores, "Total Volcados");
        var volcadosZafra = Valor(indicadores, "Volcados Zafra Actual");
        var stockPlaya = Valor(indicadores, "Stock en Playa");
        var totalPallets = Valor(indicadores, "Total Pallets");
        var palletsDespachados = Valor(indicadores, "Pallets Despachados");
        var palletsEnFrio = Valor(indicadores, "Pallets en Frío");
        var ritmo = Valor(indicadores, "Ritmo Promedio Línea");

        insight.Lectura = volcadosZafra > 0
            ? $"Acumulado de la zafra en curso: {volcadosZafra:N0} kg, sobre un histórico de {volcadosTotal:N0} kg. " +
              $"Stock en playa: {stockPlaya:N0} kg. Pallets: {totalPallets:N0} en total ({palletsDespachados:N0} despachados, {palletsEnFrio:N0} en frío)." +
              (volumenProyectadoKgs > 0
                  ? $" Próxima zafra proyectada en {volumenProyectadoKgs:N0} kg" +
                    (variacionProyectadaPct != 0 ? $" ({(variacionProyectadaPct >= 0 ? "+" : "")}{variacionProyectadaPct:N1}%)" : string.Empty) +
                    (capacidadInstaladaKgs > 0 ? $" — {volumenProyectadoKgs / capacidadInstaladaKgs * 100:N1}% de la capacidad instalada." : ".")
                  : string.Empty)
            : "Cargá o actualizá los indicadores para construir la lectura estratégica de la operación.";

        // --- Hallazgos (máx. 4) ---
        // El stock en playa está medido en KG (misma fórmula canónica que la grilla
        // Stock en Playa y Contaduría); el umbral de severidad se expresa en kg.
        if (stockPlaya < 0)
            {
            insight.Hallazgos.Add(new InsightHallazgoDto
                {
                Titulo = "Diferencia de inventario en playa",
                Detalle = $"El saldo en playa da {stockPlaya:N0} kg: hay más despachos/volcados cargados que ingresos. Reconciliar antes de reportar a Contaduría.",
                Severidad = "alta"
                });
            }
        else if (stockPlaya > 0)
            {
            insight.Hallazgos.Add(new InsightHallazgoDto
                {
                Titulo = "Fruta en espera de despacho",
                Detalle = $"{stockPlaya:N0} kg en playa: definir prioridad de empaque para no perder ventana de flete.",
                Severidad = stockPlaya > 500_000m ? "media" : "baja"
                });
            }

        if (totalPallets > 0)
            {
            var pctDespachado = palletsDespachados / totalPallets * 100;
            insight.Hallazgos.Add(new InsightHallazgoDto
                {
                Titulo = "Avance de despacho",
                Detalle = $"{palletsDespachados:N0} de {totalPallets:N0} pallets despachados ({pctDespachado:N1}%); {palletsEnFrio:N0} permanecen en frío.",
                Severidad = pctDespachado < 50 ? "media" : "baja"
                });
            }

        if (capacidadInstaladaKgs > 0 && volumenProyectadoKgs > 0)
            {
            var pct = volumenProyectadoKgs / capacidadInstaladaKgs * 100;
            insight.Hallazgos.Add(new InsightHallazgoDto
                {
                Titulo = pct >= 90 ? "Próxima zafra al tope de capacidad" : "Capacidad de la próxima zafra",
                Detalle = $"{volumenProyectadoKgs:N0} kg proyectados = {pct:N1}% de {capacidadInstaladaKgs:N0} kg instalados.",
                Severidad = pct >= 90 ? "alta" : "baja"
                });
            }

        if (ritmo > 0)
            {
            insight.Hallazgos.Add(new InsightHallazgoDto
                {
                Titulo = "Ritmo de línea",
                Detalle = $"Promedio de {ritmo:N0} kg/h por hora efectiva de calibradora en los últimos registros.",
                Severidad = "baja"
                });
            }

        // --- Acciones (máx. 4) ---
        if (stockPlaya < 0)
            {
            insight.Acciones.Add(new InsightAccionDto
                {
                Texto = "Reconciliar ingresos contra despachos/volcados y corregir el cierre de inventario",
                Plazo = "esta semana",
                Impacto = "inventario confiable para Contaduría y la proyección"
                });
            }

        if (capacidadInstaladaKgs > 0 && volumenProyectadoKgs / capacidadInstaladaKgs >= 0.9m)
            {
            insight.Acciones.Add(new InsightAccionDto
                {
                Texto = "Planificar empaque y frío de terceros para el exceso de la proyección",
                Plazo = "antes de la zafra",
                Impacto = "sin techo de volumen exportable"
                });
            }

        insight.Acciones.Add(new InsightAccionDto
            {
            Texto = "Alinear el calendario de cosecha con las ventanas de exportación (EEUU, Europa) y el mercado interno",
            Plazo = "antes de la zafra",
            Impacto = "cupos de flete y precios mejores"
            });

        insight.Acciones.Add(new InsightAccionDto
            {
            Texto = "Cerrar el mes con Contaduría para refrescar costo por kg, caja y margen",
            Plazo = "mensual",
            Impacto = "decisiones con cifras vigentes"
            });

        return insight;
        }

    /// <summary>
    /// Contexto JSON para el motor de IA: los mismos indicadores visibles en pantalla
    /// más los datos de la proyección, para que el análisis refinado no se contradiga
    /// con los números del panel.
    /// </summary>
    public static string ConstruirContextoJson(
        IReadOnlyList<IndicadorGerenciaDto> indicadores,
        decimal volumenProyectadoKgs = 0,
        decimal capacidadInstaladaKgs = 0,
        decimal variacionProyectadaPct = 0)
        {
        var contexto = new
            {
            fechaConsulta = DateTime.Today.ToString("yyyy-MM-dd"),
            empresa = "Guarino (NORIDEL SA), Salto, Uruguay — cítricos frescos: cosecha, packing, frío, exportación a EEUU y Europa y mercado interno",
            proyeccion = new
                {
                volumenProyectadoKgs,
                capacidadInstaladaKgs,
                variacionPct = variacionProyectadaPct
                },
            indicadores = indicadores.Select(i => new
                {
                indicador = i.Indicador,
                valor = i.Valor,
                // La unidad viaja en el contexto para que el análisis refinado no
                // confunda escalas (bins vs kg) al citar las cifras.
                unidad = i.Unidad,
                categoria = i.Categoria,
                fecha = i.Fecha?.ToString("yyyy-MM-dd")
                }).ToList()
            };

        return JsonSerializer.Serialize(contexto, new JsonSerializerOptions
            {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }

    private static decimal Valor(IReadOnlyList<IndicadorGerenciaDto> indicadores, string nombre)
        => indicadores.FirstOrDefault(i => string.Equals(i.Indicador, nombre, StringComparison.OrdinalIgnoreCase))?.Valor ?? 0m;
}

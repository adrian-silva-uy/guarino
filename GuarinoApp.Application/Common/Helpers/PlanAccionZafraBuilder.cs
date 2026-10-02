using GuarinoApp.Application.Common.Dtos;
using GuarinoApp.Application.Modules.Contaduria.Dtos;
using GuarinoApp.Application.Modules.Proyeccion.Dtos;

namespace GuarinoApp.Application.Common.Helpers;

/// <summary>
/// Plan de acción determinista de la proyección de zafra, compartido por la página
/// (lo pinta al instante, sin esperar a la IA) y por el servicio de respaldo (si la
/// IA no responde, el plan visible es exactamente el mismo).
/// Los ejes con dato real (Financiero, capacidad) son "Calculado"; los que todavía
/// no tienen fuente conectada (calidad, logística, mano de obra, fitosanitario,
/// costo comparado) son "Checklist" operativo estándar — nunca cifras inventadas.
/// </summary>
public static class PlanAccionZafraBuilder
    {
    public static List<BusinessSuggestionDto> Construir(
        ZafraDto proyectada,
        decimal capacidadMaxHistorica,
        int stockActualBins,
        FinancialDataDto? datos,
        FinancialKpiDto kpis)
        {
        var esSimulado = proyectada.EsDummy;
        var variacion = proyectada.VariacionPorcentual;
        var volumenProyectado = proyectada.TotalKgs;
        var acciones = new List<BusinessSuggestionDto>();

        // --- Financiero / Logística de capacidad (Calculado: datos reales disponibles acá) ---
        if (esSimulado)
            {
            acciones.Add(new BusinessSuggestionDto
                {
                Text = "⚠️ Datos de proyección son simulados. Validar con datos reales de campo antes de comprometer recursos financieros mayores.",
                Color = "#FF9800",
                Icon = "warning",
                Categoria = "Financiero",
                Origen = "Checklist",
                Riesgo = "Decisiones tomadas sobre datos simulados pueden no reflejar la realidad de campo."
                });
            }

        if (kpis.BrechaLiquidez > 0 && datos != null)
            {
            acciones.Add(new BusinessSuggestionDto
                {
                Text = $"La caja disponible no cubre el capital requerido para la zafra: faltan ${kpis.BrechaLiquidez:N0} " +
                       $"(capital ${kpis.CapitalNecesario:N0} vs. caja ${datos.FlujoCaja:N0} — riesgo {kpis.RiesgoLiquidez}). " +
                       "Gestionar crédito de temporada, adelanto de clientes o reprogramación de pagos ANTES del pico.",
                Color = "#F44336",
                Icon = "account_balance_wallet",
                Categoria = "Financiero",
                Origen = "Calculado",
                Impacto = $"Brecha de ${kpis.BrechaLiquidez:N0} a cubrir antes del inicio de zafra.",
                Riesgo = "Sin financiamiento oportuno, la zafra se frena en el pico cuando el volumen ya está comprometido con clientes."
                });
            }

        if (capacidadMaxHistorica > 0 && volumenProyectado > capacidadMaxHistorica * 1.1m)
            {
            var exceso = volumenProyectado - capacidadMaxHistorica;
            acciones.Add(new BusinessSuggestionDto
                {
                Text = $"La proyección supera la capacidad histórica en {exceso:N0} kg. Evaluar contratación de turno extra o tercerización de empaque.",
                Color = "#F44336",
                Icon = "speed",
                Categoria = "ManoDeObra",
                Origen = "Calculado",
                Impacto = $"Exceso de {exceso:N0} kg sobre capacidad histórica.",
                Riesgo = "Sin turno extra, riesgo de cuello de botella en el pico de zafra."
                });
            }
        else if (stockActualBins > 0 && volumenProyectado < capacidadMaxHistorica * 0.8m)
            {
            acciones.Add(new BusinessSuggestionDto
                {
                Text = "Volumen proyectado inferior al histórico. Optimizar turnos de empaque para reducir costos fijos y liberar capacidad de frío.",
                Color = "#2196F3",
                Icon = "trending_down",
                Categoria = "ManoDeObra",
                Origen = "Calculado"
                });
            }

        if (variacion > 10)
            {
            var incremento = (int)(stockActualBins * 0.15);
            acciones.Add(new BusinessSuggestionDto
                {
                Text = $"Crecimiento proyectado del {variacion:F1}%. Anticipar compra de insumos para {incremento} bins adicionales y asegurar precios.",
                Color = "#4CAF50",
                Icon = "shopping_cart",
                Categoria = "Financiero",
                Origen = "Calculado",
                Impacto = $"{incremento} bins adicionales de insumos estimados."
                });
            }
        else if (variacion < -5)
            {
            acciones.Add(new BusinessSuggestionDto
                {
                Text = $"Descenso proyectado del {Math.Abs(variacion):F1}%. Revisar contratos de insumos para reducir volúmenes y liberar flujo de caja.",
                Color = "#9C27B0",
                Icon = "account_balance",
                Categoria = "Financiero",
                Origen = "Calculado"
                });
            }

        // --- Ejes sin fuente de datos real conectada todavía: recordatorio operativo estándar ---
        acciones.Add(new BusinessSuggestionDto
            {
            Text = "Definir meta de packout rate para la próxima zafra y plan de calibración óptica antes del pico.",
            Color = "#795548",
            Icon = "grading",
            Categoria = "Calidad",
            Origen = "Checklist",
            Riesgo = "Sin esta revisión, la merma real puede superar lo presupuestado sin que se detecte a tiempo."
            });

        acciones.Add(new BusinessSuggestionDto
            {
            Text = "Confirmar disponibilidad de contenedores refrigerados y turnos de puerto para el volumen proyectado del pico.",
            Color = "#3F51B5",
            Icon = "local_shipping",
            Categoria = "Logistica",
            Origen = "Checklist"
            });

        acciones.Add(new BusinessSuggestionDto
            {
            Text = "Relevar disponibilidad de personal temporal en la zona antes del inicio del pico de zafra.",
            Color = "#009688",
            Icon = "groups",
            Categoria = "ManoDeObra",
            Origen = "Checklist"
            });

        acciones.Add(new BusinessSuggestionDto
            {
            Text = "Verificar vigencia de certificaciones (GlobalGAP, PPECB u otras según mercado destino) antes del inicio de zafra.",
            Color = "#8BC34A",
            Icon = "verified",
            Categoria = "Fitosanitario",
            Origen = "Checklist",
            Riesgo = "Un rechazo fitosanitario en destino invalida la proyección financiera de ese lote."
            });

        acciones.Add(new BusinessSuggestionDto
            {
            Text = "Relevar costo de flete actual por mercado de destino para poder comparar contra benchmark internacional en la próxima ronda.",
            Color = "#607D8B",
            Icon = "compare_arrows",
            Categoria = "CostoComparado",
            Origen = "Checklist"
            });

        acciones.Add(new BusinessSuggestionDto
            {
            Text = "Armar la grilla de ventanas de precio por mercado destino y el listado de requisitos de entrada (protocolo fitosanitario, certificaciones GLOBALG.A.P./BRCGS) para avanzar hacia nuevos mercados, con Asia (Vietnam, India) como franja prioritaria.",
            Color = "#00BCD4",
            Icon = "public",
            Categoria = "Mercados",
            Origen = "Checklist",
            Riesgo = "Concentrar la venta en pocos destinos deja el ingreso expuesto al precio y a la demanda de esos mercados."
            });

        return acciones;
        }
    }

using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.Proyeccion.Dtos;
using GuarinoApp.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace GuarinoApp.Application.Modules.Proyeccion.Queries.GetProyeccionZafra;

public class GetProyeccionZafraQueryHandler : IRequestHandler<GetProyeccionZafraQuery, List<ZafraDto>>
    {
    private static readonly CultureInfo CulturaMeses = CultureInfo.GetCultureInfo("es-UY");

    // Defaults SIN fuente de datos real en el sistema (no hay tabla de flota de camiones ni de
    // rendimiento de línea de empaque todavía). Son valores típicos de industria, documentados
    // acá en un solo lugar -- no un número mágico repetido en la UI. Ajustables vía los
    // parámetros opcionales de GetProyeccionZafraQuery.
    private const decimal ToneladasPorCamionDefault = 25m;
    private const decimal ToneladasPorHoraEmpaqueDefault = 10m;
    private const decimal MargenRiesgoCapacidadDefault = 10m; // %
    private const decimal KgPorBinUltimoRecurso = 20m; // solo si no hay NINGÚN Lote cargado

    private sealed record MesVolumen(int Year, int Month, decimal TotalKilos);

    private readonly IApplicationDbContext _context;

    public GetProyeccionZafraQueryHandler(IApplicationDbContext context)
        {
        _context = context;
        }

    public async Task<List<ZafraDto>> Handle(GetProyeccionZafraQuery request, CancellationToken cancellationToken)
        {
        try
            {
            // la misma que usa lotesvolcados.razor
            var historico = await _context.LotesEmboquille
                .Where(le => !le.Lote.IsDeleted)
                .GroupBy(le => le.FechaProduccion.Year)
                .Select(g => new { Año = g.Key, Total = g.Sum(le => le.KilosNetos) })
                .OrderBy(x => x.Año)
                .ToListAsync(cancellationToken);

            if (historico.Any())
                {
                var datos = historico.Select(h => (Año: h.Año, Total: h.Total)).ToList();
                return await GenerarZafras(datos, request, cancellationToken);
                }

            return await GenerarZafrasDummy(request, cancellationToken);
            }
        catch (Exception)
            {
            return await GenerarZafrasDummy(request, cancellationToken);
            }
        }

    private async Task<List<ZafraDto>> GenerarZafras(
        List<(int Año, decimal Total)> historico,
        GetProyeccionZafraQuery request,
        CancellationToken cancellationToken)
        {
        var volcadosPorMes = await _context.LotesEmboquille
            .Where(le => !le.Lote.IsDeleted)
            .GroupBy(le => new { le.FechaProduccion.Year, le.FechaProduccion.Month })
            .Select(g => new MesVolumen(g.Key.Year, g.Key.Month, g.Sum(le => le.KilosNetos)))
            .ToListAsync(cancellationToken);

        var zafras = new List<ZafraDto>();
        var variacionesHistoricas = new List<decimal>();

        for (int i = 0; i < historico.Count; i++)
            {
            var año = historico[i].Año;
            var variacion = i > 0 ? ((historico[i].Total - historico[i - 1].Total) / historico[i - 1].Total * 100) : 0;
            if (i > 0) variacionesHistoricas.Add(variacion);

            var mesesDelAño = volcadosPorMes.Where(m => m.Year == año).ToList();
            var esZafraActual = i == historico.Count - 1;

            zafras.Add(new ZafraDto
                {
                Año = año,
                TotalKgs = historico[i].Total,
                VariacionPorcentual = Math.Round(variacion, 1),
                MesesPico = CalcularMesesPicoDelAño(mesesDelAño),
                NumeroMeses = mesesDelAño.Count,
                Estado = esZafraActual ? EstadoZafra.Actual.ToString() : EstadoZafra.Historico.ToString(),
                Comentario = esZafraActual
                    ? (mesesDelAño.Count < 12
                        ? $"Zafra en curso — datos reales de {mesesDelAño.Count} de 12 meses"
                        : "Zafra en curso")
                    : "Datos históricos reales de volcado",
                EsDummy = false
                });
            }

        if (!zafras.Any())
            return zafras;

        decimal factorCrecimiento;
        string comentarioMetodo;

        if (request.PorcentajeCrecimientoManual.HasValue)
            {
            factorCrecimiento = request.PorcentajeCrecimientoManual.Value;
            comentarioMetodo = $"Escenario manual ingresado por el usuario: +{factorCrecimiento:F1}%.";
            }
        else if (variacionesHistoricas.Any())
            {
            factorCrecimiento = Math.Round(variacionesHistoricas.Average(), 1);
            var detalleAnios = string.Join(", ", zafras.Skip(1)
                .Select(z => $"{z.Año}: {(z.VariacionPorcentual >= 0 ? "+" : "")}{z.VariacionPorcentual:F1}%"));
            comentarioMetodo = $"Promedio de variación interanual real ({detalleAnios}) → +{factorCrecimiento:F1}%.";
            }
        else
            {
            factorCrecimiento = 0m;
            comentarioMetodo = "Sin suficiente histórico para calcular una tendencia (un solo año registrado). Se proyecta sin crecimiento asumido — ingresá un % manual si tenés una expectativa distinta.";
            }

        var picoTipico = volcadosPorMes
            .GroupBy(m => m.Month)
            .Select(g => new { Month = g.Key, Total = g.Sum(x => x.TotalKilos) })
            .OrderByDescending(x => x.Total)
            .Take(4)
            .Select(x => x.Month)
            .OrderBy(m => m)
            .ToList();

        var ultima = zafras.Last();
        var totalProyectado = Math.Round(ultima.TotalKgs * (1 + factorCrecimiento / 100), 0);
        var capacidadMaxHistorica = zafras.Select(z => z.TotalKgs).DefaultIfEmpty(0m).Max();

        var (impacto, supuestos) = await CalcularImpactoOperativoAsync(totalProyectado, capacidadMaxHistorica, request, cancellationToken);

        zafras.Add(new ZafraDto
            {
            Año = ultima.Año + 1,
            TotalKgs = totalProyectado,
            VariacionPorcentual = Math.Round(factorCrecimiento, 1),
            MesesPico = picoTipico.Any()
                ? $"{FormatearMeses(picoTipico)} (estimado según patrón histórico)"
                : "Sin datos suficientes para estimar",
            NumeroMeses = 12,
            Estado = "Proyectado",
            Comentario = comentarioMetodo,
            EsDummy = false,
            BinsEstimados = impacto.Bins,
            CamionesEstimados = impacto.Camiones,
            HorasEmpaqueEstimadas = impacto.Horas,
            RiesgoCapacidad = impacto.Riesgo,
            CapacidadMaximaHistoricaKgs = capacidadMaxHistorica,
            SupuestosOperativos = supuestos
            });

        return zafras;
        }

    private async Task<List<ZafraDto>> GenerarZafrasDummy(GetProyeccionZafraQuery request, CancellationToken cancellationToken)
        {
        var random = new Random(2026827);
        var zafras = new List<ZafraDto>();
        var añoActual = DateTime.Now.Year;
        var variacionesDummy = new List<decimal>();

        var mesesDummyPorAño = new[]
            {
            new[] { 3, 6, 9, 12 },
            new[] { 2, 5, 8, 11 },
            new[] { 3, 7, 9, 12 },
            new[] { 4, 6, 9, 11 }
        };

        for (int año = añoActual - 3; año <= añoActual; año++)
            {
            var idx = año - (añoActual - 3);
            var total = random.Next(150000, 250000);
            var variacionHist = idx > 0 ? Math.Round((decimal)(random.Next(-5, 20)), 1) : 0;
            if (idx > 0) variacionesDummy.Add(variacionHist);

            zafras.Add(new ZafraDto
                {
                Año = año,
                TotalKgs = total,
                VariacionPorcentual = variacionHist,
                MesesPico = $"{FormatearMeses(mesesDummyPorAño[idx])} (simulado)",
                NumeroMeses = 12,
                Estado = año == añoActual ? EstadoZafra.Actual.ToString() : EstadoZafra.Historico.ToString(),
                Comentario = año == añoActual
                    ? "Zafra en curso (datos de ejemplo — no hay registros históricos en el sistema)"
                    : "Datos de ejemplo (no hay registros históricos en el sistema)",
                EsDummy = true
                });
            }

        var ultimoTotal = zafras.Last().TotalKgs;
        decimal factorCrecimiento;
        string comentarioMetodo;

        if (request.PorcentajeCrecimientoManual.HasValue)
            {
            factorCrecimiento = request.PorcentajeCrecimientoManual.Value;
            comentarioMetodo = $"Escenario manual ingresado por el usuario: +{factorCrecimiento:F1}% (aplicado sobre datos de ejemplo).";
            }
        else
            {
            factorCrecimiento = variacionesDummy.Any() ? Math.Round(variacionesDummy.Average(), 1) : 0m;
            comentarioMetodo = $"Promedio de variación simulada (+{factorCrecimiento:F1}%) — sin datos históricos reales para calcular tendencias.";
            }

        var totalProyectado = Math.Round(ultimoTotal * (1 + factorCrecimiento / 100), 0);
        var capacidadMaxHistorica = zafras.Select(z => z.TotalKgs).DefaultIfEmpty(0m).Max();

        var (impacto, supuestos) = await CalcularImpactoOperativoAsync(totalProyectado, capacidadMaxHistorica, request, cancellationToken);

        zafras.Add(new ZafraDto
            {
            Año = añoActual + 1,
            TotalKgs = totalProyectado,
            VariacionPorcentual = Math.Round(factorCrecimiento, 1),
            MesesPico = $"{FormatearMeses(mesesDummyPorAño[0])} (estimado, simulado)",
            NumeroMeses = 12,
            Estado = EstadoZafra.Proyectado.ToString(),
            Comentario = comentarioMetodo,
            EsDummy = true,
            BinsEstimados = impacto.Bins,
            CamionesEstimados = impacto.Camiones,
            HorasEmpaqueEstimadas = impacto.Horas,
            RiesgoCapacidad = impacto.Riesgo,
            CapacidadMaximaHistoricaKgs = capacidadMaxHistorica,
            SupuestosOperativos = supuestos
            });

        return zafras;
        }

    /// <summary>
    /// Calcula bins/camiones/horas de empaque estimados para el volumen proyectado. kg/bin sale
    /// del promedio REAL de Lotes cargados (KilosNetos / CantidadBins) salvo que el usuario lo
    /// override; camión/hora de empaque no tienen fuente de datos real en el sistema todavía, así
    /// que usan el parámetro del usuario o el default documentado -- nunca un número inventado en
    /// silencio, siempre queda explicado en "supuestos".
    /// </summary>
    private async Task<((decimal Bins, decimal Camiones, decimal Horas, bool Riesgo) Impacto, string Supuestos)>
        CalcularImpactoOperativoAsync(
            decimal totalProyectadoKgs,
            decimal capacidadMaxHistoricaKgs,
            GetProyeccionZafraQuery request,
            CancellationToken cancellationToken)
        {
        decimal kgPorBin;
        string origenKgPorBin;

        if (request.KgPromedioPorBin.HasValue)
            {
            kgPorBin = request.KgPromedioPorBin.Value;
            origenKgPorBin = "parámetro manual";
            }
        else
            {
            var valoresPorLote = await _context.Lotes
                .Where(l => l.CantidadBins > 0)
                .Select(l => l.KilosNetos / l.CantidadBins)
                .ToListAsync(cancellationToken);

            if (valoresPorLote.Any())
                {
                kgPorBin = Math.Round(valoresPorLote.Average(), 1);
                origenKgPorBin = $"promedio real de {valoresPorLote.Count} lotes";
                }
            else
                {
                kgPorBin = KgPorBinUltimoRecurso;
                origenKgPorBin = "sin lotes cargados aún — valor de referencia de industria";
                }
            }

        var tnPorCamion = request.ToneladasPorCamion ?? ToneladasPorCamionDefault;
        var origenCamion = request.ToneladasPorCamion.HasValue ? "parámetro manual" : "default (sin datos de flota registrados)";

        var tnPorHora = request.ToneladasPorHoraEmpaque ?? ToneladasPorHoraEmpaqueDefault;
        var origenHora = request.ToneladasPorHoraEmpaque.HasValue ? "parámetro manual" : "default (sin datos de rendimiento de línea registrados)";

        var margenRiesgo = request.MargenRiesgoCapacidadPorcentual ?? MargenRiesgoCapacidadDefault;

        var bins = kgPorBin > 0 ? Math.Round(totalProyectadoKgs / kgPorBin, 0) : 0m;
        var camiones = tnPorCamion > 0 ? Math.Round(totalProyectadoKgs / (tnPorCamion * 1000m), 1) : 0m;
        var horas = tnPorHora > 0 ? Math.Round(totalProyectadoKgs / (tnPorHora * 1000m), 1) : 0m;
        var riesgo = capacidadMaxHistoricaKgs > 0 && totalProyectadoKgs > capacidadMaxHistoricaKgs * (1 + margenRiesgo / 100);

        var supuestos = $"{kgPorBin:0.0} kg/bin ({origenKgPorBin}), {tnPorCamion:0.#} tn/camión ({origenCamion}), " +
                        $"{tnPorHora:0.#} tn/hora de empaque ({origenHora}), margen de riesgo {margenRiesgo:0.#}%.";

        return ((bins, camiones, horas, riesgo), supuestos);
        }

    private static string CalcularMesesPicoDelAño(List<MesVolumen> mesesDelAño)
        {
        if (mesesDelAño.Count == 0)
            return "Sin datos suficientes";

        var picos = mesesDelAño
            .OrderByDescending(m => m.TotalKilos)
            .Take(Math.Min(4, mesesDelAño.Count))
            .Select(m => m.Month)
            .OrderBy(m => m)
            .ToList();

        return FormatearMeses(picos);
        }

    private static string FormatearMeses(IEnumerable<int> meses)
        {
        return string.Join(", ", meses.Select(m =>
        {
            var nombre = new DateTime(2000, m, 1).ToString("MMMM", CulturaMeses);
            return char.ToUpper(nombre[0], CulturaMeses) + nombre[1..];
        }));
        }
    }

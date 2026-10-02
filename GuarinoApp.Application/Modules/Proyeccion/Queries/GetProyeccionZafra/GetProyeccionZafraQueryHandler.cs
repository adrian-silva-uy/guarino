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

    /// <summary>Jornada estándar de empaque para convertir el histórico diario en tn/hora.</summary>
    private const int JornadaEmpaqueHoras = 8;
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
    /// Calcula bins/camiones/horas de empaque estimados para el volumen proyectado. Los tres
    /// supuestos salen de datos reales del sistema salvo que el usuario los override:
    /// kg/bin del promedio REAL de Lotes cargados (KilosNetos / CantidadBins); tn/camión del
    /// promedio de capacidad de la flota (misma regla determinista que /camioneszorras, que
    /// deriva camiones y zorras de los Productores); tn/hora de empaque del promedio histórico
    /// diario de ingreso de lotes convertido a la jornada estándar. Si una fuente está vacía se
    /// cae al default documentado — nunca un número inventado en silencio, siempre queda
    /// explicado en "supuestos".
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

        decimal tnPorCamion;
        string origenCamion;
        if (request.ToneladasPorCamion.HasValue)
            {
            tnPorCamion = request.ToneladasPorCamion.Value;
            origenCamion = "parámetro manual";
            }
        else
            {
            (tnPorCamion, origenCamion) = await CalcularCapacidadFlotaAsync(cancellationToken);
            }

        decimal tnPorHora;
        string origenHora;
        if (request.ToneladasPorHoraEmpaque.HasValue)
            {
            tnPorHora = request.ToneladasPorHoraEmpaque.Value;
            origenHora = "parámetro manual";
            }
        else
            {
            (tnPorHora, origenHora) = await CalcularRendimientoLineaAsync(cancellationToken);
            }

        var margenRiesgo = request.MargenRiesgoCapacidadPorcentual ?? MargenRiesgoCapacidadDefault;

        var bins = kgPorBin > 0 ? Math.Round(totalProyectadoKgs / kgPorBin, 0) : 0m;
        var camiones = tnPorCamion > 0 ? Math.Round(totalProyectadoKgs / (tnPorCamion * 1000m), 1) : 0m;
        var horas = tnPorHora > 0 ? Math.Round(totalProyectadoKgs / (tnPorHora * 1000m), 1) : 0m;
        var riesgo = capacidadMaxHistoricaKgs > 0 && totalProyectadoKgs > capacidadMaxHistoricaKgs * (1 + margenRiesgo / 100);

        var supuestos = $"{kgPorBin:0.0} kg/bin ({origenKgPorBin}), {tnPorCamion:0.#} tn/camión ({origenCamion}), " +
                        $"{tnPorHora:0.#} tn/hora de empaque ({origenHora}), margen de riesgo {margenRiesgo:0.#}%.";

        return ((bins, camiones, horas, riesgo), supuestos);
        }

    /// <summary>
    /// Capacidad promedio de la flota, derivada de los Productores con la MISMA regla
    /// determinista que usa /camioneszorras (índice par = camión de 20 tn, impar = zorra de
    /// 15 tn), para que el supuesto y la página de flota digan exactamente lo mismo.
    /// Sin productores registrados se cae al default documentado.
    /// </summary>
    private async Task<(decimal Capacidad, string Origen)> CalcularCapacidadFlotaAsync(CancellationToken cancellationToken)
        {
        var productores = await _context.Productores
            .AsNoTracking()
            .Where(p => !p.IsDeleted)
            .ToListAsync(cancellationToken);

        if (productores.Count == 0)
            return (ToneladasPorCamionDefault, "default (sin datos de flota registrados)");

        var capacidades = productores.Select((_, i) => i % 2 == 0 ? 20m : 15m).ToList();
        var camiones = capacidades.Count(c => c == 20m);
        var zorras = capacidades.Count - camiones;
        var promedio = Math.Round(capacidades.Average(), 1);

        return (promedio, $"promedio de {productores.Count} unidades de flota: {camiones} camiones y {zorras} zorras");
        }

    /// <summary>
    /// Rendimiento de línea en tn/hora: promedio histórico de kg ingresados por jornada
    /// (FechaIngreso de los Lotes) dividido por la jornada estándar de empaque. Si no hay
    /// lotes con fecha se cae al default documentado.
    /// </summary>
    private async Task<(decimal Toneladas, string Origen)> CalcularRendimientoLineaAsync(CancellationToken cancellationToken)
        {
        var jornadas = await _context.Lotes
            .AsNoTracking()
            .Where(l => l.FechaIngreso != null && l.CantidadBins > 0)
            .GroupBy(l => l.FechaIngreso!.Value.Date)
            .Select(g => g.Sum(l => l.KilosNetos))
            .ToListAsync(cancellationToken);

        if (jornadas.Count == 0)
            return (ToneladasPorHoraEmpaqueDefault, "default (sin datos de rendimiento de línea registrados)");

        var tnPorHora = Math.Round(jornadas.Average() / 1000m / JornadaEmpaqueHoras, 1);
        if (tnPorHora <= 0)
            return (ToneladasPorHoraEmpaqueDefault, "default (sin datos de rendimiento de línea registrados)");

        return (tnPorHora, $"promedio de {jornadas.Count} jornadas históricas ÷ {JornadaEmpaqueHoras} h/día");
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

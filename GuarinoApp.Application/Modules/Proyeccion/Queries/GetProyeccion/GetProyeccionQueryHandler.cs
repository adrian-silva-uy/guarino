using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.Proyeccion.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GuarinoApp.Application.Modules.Proyeccion.Queries.GetProyeccion
    {
    public class GetProyeccionQueryHandler : IRequestHandler<GetProyeccionQuery, GetProyeccionResultDto>
        {
        // Reglas de negocio de la proyección
        private const int MesesHistoricoAMostrar = 12;
        private const int HorizonteProyeccionMeses = 6;
        private const int VentanaPromedioMovilMeses = 3;

        private static readonly string[] Meses =
            { "Ene", "Feb", "Mar", "Abr", "May", "Jun", "Jul", "Ago", "Sep", "Oct", "Nov", "Dic" };

        private readonly IApplicationDbContext _context;
        private readonly ILogger<GetProyeccionQueryHandler> _logger;

        public GetProyeccionQueryHandler(IApplicationDbContext context, ILogger<GetProyeccionQueryHandler> logger)
            {
            _context = context;
            _logger = logger;
            }

        public async Task<GetProyeccionResultDto> Handle(GetProyeccionQuery request, CancellationToken cancellationToken)
            {
            try
                {
                // FIX: antes se armaba el record HistoricoMensual (con el Sum agregado) DENTRO del
                // Select que EF Core intenta traducir a SQL -- eso es lo que tiraba
                // "The LINQ expression ... could not be translated". Se separa en dos pasos:
                // 1) proyección a un tipo anónimo (sí traduce) + ToListAsync, 2) mapeo a
                // HistoricoMensual en memoria (cliente), igual que ya se hace en
                // GetProyeccionZafraQueryHandler.
                var historicoRaw = await _context.LotesEmboquille
                    .GroupBy(l => new { l.FechaProduccion.Year, l.FechaProduccion.Month })
                    .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(l => l.KilosNetos) })
                    .OrderBy(x => x.Year)
                    .ThenBy(x => x.Month)
                    .ToListAsync(cancellationToken);

                var historico = historicoRaw
                    .Select(x => new HistoricoMensual(x.Year, x.Month, x.Total))
                    .ToList();

                if (historico.Any())
                    {
                    return new GetProyeccionResultDto
                        {
                        Datos = GenerateProjection(historico),
                        EsDummy = false
                        };
                    }

                _logger.LogInformation("No hay datos históricos de producción en LotesEmboquille todavía.");
                return SinDatos();
                }
            catch (Exception ex)
                {
                _logger.LogError(ex, "Error obteniendo proyección real de LotesEmboquille.");
                return SinDatos();
                }
            }

        // FIX: antes esto era GenerateDummyProjection(), que devolvía kilos INVENTADOS con
        // random.Next(35000, 85000) (sin siquiera semilla fija -- cambiaban en cada carga de
        // página) presentados como si fueran una proyección real. Eso viola el principio del
        // proyecto de nunca rellenar con promedios/datos inventados. Ahora, sin datos reales,
        // se devuelve una lista vacía + EsDummy=true, para que la UI muestre explícitamente
        // "sin datos históricos suficientes" en vez de un gráfico con números falsos.
        private static GetProyeccionResultDto SinDatos() => new()
            {
            Datos = new List<ProyeccionDto>(),
            EsDummy = true
            };

        private static string FormatPeriodo(int mes, int año) => $"{Meses[mes - 1]} {año}";

        private List<ProyeccionDto> GenerateProjection(List<HistoricoMensual> historico)
            {
            var data = new List<ProyeccionDto>();
            var ultimosMeses = historico.TakeLast(MesesHistoricoAMostrar).ToList();

            foreach (var h in ultimosMeses)
                {
                data.Add(new ProyeccionDto
                    {
                    Año = h.Año,
                    Mes = h.Mes,
                    Periodo = FormatPeriodo(h.Mes, h.Año),
                    KgsVolcados = h.KgsVolcados,
                    KgsProyectados = h.KgsVolcados,
                    Tendencia = 0,
                    EsProyectado = false
                    });
                }

            var ultimo = ultimosMeses.Last();
            var ventana = ultimosMeses.TakeLast(VentanaPromedioMovilMeses).Select(h => h.KgsVolcados).ToList();
            var valorAnterior = ultimo.KgsVolcados;

            int mesBase = ultimo.Mes;
            int añoBase = ultimo.Año;

            for (int i = 1; i <= HorizonteProyeccionMeses; i++)
                {
                int mesProx = mesBase + i;
                int m = ((mesProx - 1) % 12) + 1;
                int y = añoBase + (mesBase + i - 1) / 12;

                var promedio = ventana.Count > 0 ? ventana.Average() : 0;
                var tendencia = valorAnterior > 0 ? Math.Round((promedio - valorAnterior) / valorAnterior * 100, 1) : 0;

                data.Add(new ProyeccionDto
                    {
                    Año = y,
                    Mes = m,
                    Periodo = FormatPeriodo(m, y),
                    KgsVolcados = 0,
                    KgsProyectados = Math.Round(promedio, 0),
                    Tendencia = tendencia,
                    EsProyectado = true
                    });

                ventana.Add(promedio);
                if (ventana.Count > VentanaPromedioMovilMeses) ventana.RemoveAt(0);
                valorAnterior = promedio;
                }

            return data;
            }

        private sealed record HistoricoMensual(int Año, int Mes, decimal KgsVolcados);
        }
    }

using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.ResumenMensual.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GuarinoApp.Application.Modules.ResumenMensual.Queries.GetResumenMensual;
public class GetResumenMensualQueryHandler : IRequestHandler<GetResumenMensualQuery, List<ResumenMensualDto>>
    {
    private readonly IApplicationDbContext _context;

    public GetResumenMensualQueryHandler(IApplicationDbContext context)
        {
        _context = context;
        }

    public async Task<List<ResumenMensualDto>> Handle(GetResumenMensualQuery request, CancellationToken cancellationToken)
        {
        try
            {
            // Límites por MES completos: el picker dice "primer/último mes a resumir",
            // así que 15/05 incluye mayo entero (desde el día 1) y el hasta incluye
            // el mes elegido completo (límite exclusivo = primer día del mes siguiente).
            var desdeLimite = request.FechaDesde.HasValue
                ? new DateTime(request.FechaDesde.Value.Year, request.FechaDesde.Value.Month, 1)
                : (DateTime?)null;
            var hastaLimite = request.FechaHasta.HasValue
                ? new DateTime(request.FechaHasta.Value.Year, request.FechaHasta.Value.Month, 1).AddMonths(1)
                : (DateTime?)null;

            var meses = await _context.LotesEmboquille
                .AsNoTracking()
                .Where(le => !le.Lote.IsDeleted
                    && (!desdeLimite.HasValue || le.FechaProduccion >= desdeLimite.Value)
                    && (!hastaLimite.HasValue || le.FechaProduccion < hastaLimite.Value))
                .GroupBy(le => new { le.FechaProduccion.Year, le.FechaProduccion.Month })
                .Select(g => new
                    {
                    g.Key.Year,
                    g.Key.Month,
                    Kgs_Volcados = g.Sum(le => le.KilosNetos),
                    BinsVolcados = g.Sum(le => le.CantidadBins)
                    })
                .OrderBy(x => x.Year)
                .ThenBy(x => x.Month)
                .ToListAsync(cancellationToken);

            if (meses.Count == 0)
                return new List<ResumenMensualDto>();

            // Básculas reales por mes (SELECT DISTINCT en SQL): un mes puede volcar en
            // varias básculas, por eso la celda las lista todas (la página muestra
            // "N básculas" con el detalle al pasar el mouse). Nunca dato inventado.
            var balanzasMes = await _context.LotesEmboquille
                .AsNoTracking()
                .Where(le => !le.Lote.IsDeleted
                    && (!desdeLimite.HasValue || le.FechaProduccion >= desdeLimite.Value)
                    && (!hastaLimite.HasValue || le.FechaProduccion < hastaLimite.Value))
                .Select(le => new
                    {
                    le.FechaProduccion.Year,
                    le.FechaProduccion.Month,
                    Nombre = le.Bascula != null ? le.Bascula.Descripcion : null
                    })
                .Distinct()
                .ToListAsync(cancellationToken);

            return meses.Select(m => new ResumenMensualDto
                {
                Año = m.Year,
                Mes = m.Month,
                Nombre = new DateTime(m.Year, m.Month, 1).ToString("MMMM"),
                Kgs_Volcados = m.Kgs_Volcados,
                BinsVolcados = m.BinsVolcados,
                Balanza = string.Join(", ", balanzasMes
                    .Where(b => b.Year == m.Year && b.Month == m.Month && b.Nombre != null)
                    .Select(b => b.Nombre!)
                    .Distinct()
                    .OrderBy(n => n))
                })
                .ToList();
            }
        catch
            {
            return new List<ResumenMensualDto>();
            }
        }

    private List<ResumenMensualDto> GetDummyData(DateTime? fechaDesde, DateTime? fechaHasta)
        {
        var random = new Random();
        var meses = new[] { "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre" };
        var data = new List<ResumenMensualDto>();

        int year = DateTime.Now.Year;
        int mesInicio = 1;
        int mesFin = 12;

        if (fechaDesde.HasValue)
            mesInicio = fechaDesde.Value.Month;
        if (fechaHasta.HasValue)
            mesFin = fechaHasta.Value.Month;

        for (int mes = mesInicio; mes <= mesFin; mes++)
            {
            if (mes > DateTime.Now.Month && year == DateTime.Now.Year)
                break;

            data.Add(new ResumenMensualDto
                {
                Año = year,
                Mes = mes,
                Nombre = meses[mes - 1],
                Kgs_Volcados = random.Next(15000, 85000),
                BinsVolcados = random.Next(100, 500),
                Balanza = mes % 2 == 0 ? "Báscula 3 - Exportación" : "Báscula 2 - Empaque"
                });
            }
        return data;
        }
    }

using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.LotesVolcados.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GuarinoApp.Application.Modules.LotesVolcados.Queries.GetLotesVolcados;

public class GetLotesVolcadosQueryHandler : IRequestHandler<GetLotesVolcadosQuery, LotesVolcadosResultDto>
    {
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetLotesVolcadosQueryHandler> _logger;

    public GetLotesVolcadosQueryHandler(
        IApplicationDbContext context,
        ILogger<GetLotesVolcadosQueryHandler> logger)
        {
        _context = context;
        _logger = logger;
        }

    public async Task<LotesVolcadosResultDto> Handle(GetLotesVolcadosQuery request, CancellationToken cancellationToken)
        {
        try
            {
            // Filtro por rango de fechas (opcional): si no se pasan fechas, se devuelve el histórico completo.
            var query = _context.LotesEmboquille
                .Include(le => le.Lote)
                    .ThenInclude(l => l.Variedad)
                .Include(le => le.Lote)
                    .ThenInclude(l => l.Productor)
                .Where(le => !le.Lote.IsDeleted
                    && (!request.FechaDesde.HasValue || le.FechaProduccion.Date >= request.FechaDesde.Value.Date)
                    && (!request.FechaHasta.HasValue || le.FechaProduccion.Date <= request.FechaHasta.Value.Date))
                .GroupBy(le => new
                    {
                    le.Lote.Id,
                    le.Lote.Codigo,
                    le.Lote.Variedad.Nombre,
                    le.Lote.Productor.RazonSocial,
                    le.FechaProduccion
                    })
                .Select(g => new LoteVolcadoDto
                    {
                    IdLote = g.Key.Id,
                    Codigo = g.Key.Codigo,
                    Variedad = g.Key.Nombre,
                    Productor = g.Key.RazonSocial,
                    FechaVolcado = g.Key.FechaProduccion,
                    KilosVolcados = g.Sum(le => le.KilosNetos),
                    BinsVolcados = g.Sum(le => le.CantidadBins)
                    })
                .AsNoTracking()
                .OrderByDescending(g => g.FechaVolcado);

            var result = await query.ToListAsync(cancellationToken);

            if (result.Count == 0)
                {
                // Los datos de ejemplo solo corresponden si la tabla está vacía (BD sin datos reales).
                // Un rango de fechas sin resultados NO es dato demo: se devuelve vacío para que
                // la página informe "sin registros para el rango".
                var hayDatosReales = await _context.LotesEmboquille.AsNoTracking().AnyAsync(cancellationToken);
                if (!hayDatosReales)
                    return new LotesVolcadosResultDto(GetDummyData(), EsDummy: true);
                }

            return new LotesVolcadosResultDto(result, EsDummy: false);
            }
        catch (Exception ex)
            {
            _logger.LogError(ex, "Error consultando lotes volcados vía EF Core; devolviendo datos de ejemplo.");
            return new LotesVolcadosResultDto(GetDummyData(), EsDummy: true);
            }
        }

    private List<LoteVolcadoDto> GetDummyData()
        {
        var random = new Random();
        var variedades = new[] { "Valencia", "Navel", "Satsuma", "Murcott", "Eureka", "Star Ruby" };
        var productores = new[] { "Frutas del Sol", "Agrícola San Juan", "Cítricos del Valle", "Huerta San Rafael" };
        var data = new List<LoteVolcadoDto>();

        for (int i = 1; i <= 20; i++)
            {
            var bins = random.Next(20, 150);
            data.Add(new LoteVolcadoDto
                {
                IdLote = 5000 + i,
                Codigo = $"LOTE-{DateTime.Now.Year}-{5000 + i}",
                Variedad = variedades[random.Next(variedades.Length)],
                Productor = productores[random.Next(productores.Length)],
                FechaVolcado = DateTime.Now.AddDays(-random.Next(1, 90)),
                KilosVolcados = bins * random.Next(15, 22),
                BinsVolcados = bins
                });
            }

        return data;
        }
    }

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

            // Cero datos inventados: si el rango está vacío o la tabla no tiene
            // filas, se devuelve vacío real (la página informa "sin registros").
            // Nunca se rellena con ejemplos: en Demo/Prod los números deben
            // salir sí o sí de la base de datos de DTrace.
            return new LotesVolcadosResultDto(result, EsDummy: false);
            }
        catch (Exception ex)
            {
            // En error se devuelve vacío + log: mostrar datos de ejemplo ante un
            // fallo de BD ocultaría el problema y rompería la coherencia de cifras.
            _logger.LogError(ex, "Error consultando lotes volcados vía EF Core; se devuelve vacío (sin datos de ejemplo).");
            return new LotesVolcadosResultDto(new List<LoteVolcadoDto>(), EsDummy: false);
            }
        }
    }

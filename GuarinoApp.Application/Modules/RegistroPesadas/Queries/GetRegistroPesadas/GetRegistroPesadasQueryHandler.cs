using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.RegistroPesadas.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GuarinoApp.Application.Modules.RegistroPesadas.Queries.GetRegistroPesadas;

public class GetRegistroPesadasQueryHandler : IRequestHandler<GetRegistroPesadasQuery, RegistroPesadasResultDto>
    {
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetRegistroPesadasQueryHandler> _logger;

    public GetRegistroPesadasQueryHandler(
        IApplicationDbContext context,
        ILogger<GetRegistroPesadasQueryHandler> logger)
        {
        _context = context;
        _logger = logger;
        }

    public async Task<RegistroPesadasResultDto> Handle(GetRegistroPesadasQuery request, CancellationToken cancellationToken)
        {
        try
            {

            // Joins explícitos sobre las FK normalizadas: LoteId (antes el LEFT
            // JOIN de báscula apuntaba a b.IdBascula, columna muerta de la
            // maestra que siempre vale 0, y nunca matcheaba).
            var query = from le in _context.LotesEmboquille.AsNoTracking()
                        join l in _context.Lotes.AsNoTracking() on le.LoteId equals l.Id
                        join p in _context.Productores.AsNoTracking() on l.ProductorId equals p.Id
                        join b in _context.Basculas.AsNoTracking() on le.IdBascula equals b.Id into bg
                        from b in bg.DefaultIfEmpty() // LEFT JOIN para Bascula (puede ser null)
                        where (!request.FechaDesde.HasValue || le.FechaProduccion.Date >= request.FechaDesde.Value.Date)
                           && (!request.FechaHasta.HasValue || le.FechaProduccion.Date <= request.FechaHasta.Value.Date)
                        orderby le.FechaProduccion descending // LotesEmboquille tiene FechaVolcado corresponde a FechaProduccion
                        select new RegistroPesadaDto
                            {
                            Id = le.Id,
                            Fecha = le.FechaProduccion, 
                            Lote = l.Codigo ?? "N/A",
                            Productor = p.RazonSocial ?? "Desconocido",
                            Bascula = b != null ? b.Descripcion : "Sin asignar",
                            Peso = le.KilosNetos,
                            Bins = le.CantidadBins 
                            };

            var result = await query.ToListAsync(cancellationToken);

            // Cero datos inventados: tabla vacía o rango vacío → vacío real.
            // Nunca datos de ejemplo: en Demo/Prod todo sale de DTrace.
            _logger.LogInformation("Se encontraron {Count} registros de pesada desde LotesEmboquille.", result.Count);
            return new RegistroPesadasResultDto(result, EsDummy: false);
            }
        catch (Exception ex)
            {
            // Vacío + log ante error de BD (nunca ejemplos: ocultarían el fallo
            // y romperían la coherencia de cifras con la base real).
            _logger.LogError(ex, "Error al consultar registros de pesada desde LotesEmboquille. Se devuelve vacío (sin datos de ejemplo).");
            return new RegistroPesadasResultDto(new List<RegistroPesadaDto>(), EsDummy: false);
            }
        }
    }

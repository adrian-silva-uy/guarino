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

            if (result.Count == 0)
                {
                // Datos de ejemplo solo si la tabla está vacía (BD sin datos reales);
                // un rango de fechas sin resultados no es dato demo y se devuelve vacío.
                var hayDatosReales = await _context.LotesEmboquille.AsNoTracking().AnyAsync(cancellationToken);
                if (!hayDatosReales)
                    {
                    _logger.LogInformation("No se encontraron registros de pesada en LotesEmboquille. Retornando datos dummy.");
                    return new RegistroPesadasResultDto(GetDummyData(), EsDummy: true);
                    }
                }

            _logger.LogInformation("Se encontraron {Count} registros de pesada desde LotesEmboquille.", result.Count);
            return new RegistroPesadasResultDto(result, EsDummy: false);
            }
        catch (Exception ex)
            {
            _logger.LogError(ex, "Error al consultar registros de pesada desde LotesEmboquille. Retornando datos dummy.");
            return new RegistroPesadasResultDto(GetDummyData(), EsDummy: true);
            }
        }

    private List<RegistroPesadaDto> GetDummyData()
        {
        var random = new Random();
        var productores = new[] { "Frutas del Sol", "Agrícola San Juan", "Viña del Valle" };
        var basculas = new[] { "Báscula 1 - Patio", "Báscula 2 - Empaque", "Báscula 3 - Exportación" };
        var data = new List<RegistroPesadaDto>();

        for (int i = 1; i <= 30; i++)
            {
            data.Add(new RegistroPesadaDto
                {
                Id = 1000 + i,
                Fecha = DateTime.Now.AddHours(-random.Next(1, 72)),
                Lote = $"L{1000 + i}",
                Productor = productores[random.Next(productores.Length)],
                Bascula = basculas[random.Next(basculas.Length)],
                Peso = random.Next(500, 3000),
                Bins = random.Next(10, 40)
                });
            }
        return data;
        }
    }

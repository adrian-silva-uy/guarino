using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.Empaque.Dtos;
using GuarinoApp.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GuarinoApp.Application.Modules.Empaque.Queries.GetEmpaque;

public class GetEmpaqueQueryHandler : IRequestHandler<GetEmpaqueQuery, List<EmpaqueDto>>
    {
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetEmpaqueQueryHandler> _logger;

    public GetEmpaqueQueryHandler(
        IApplicationDbContext context,
        ILogger<GetEmpaqueQueryHandler> logger)
        {
        _context = context;
        _logger = logger;
        }

    public async Task<List<EmpaqueDto>> Handle(GetEmpaqueQuery request, CancellationToken cancellationToken)
        {
        try
            {
            var queryResult = await _context.Pallets
                .AsNoTracking()

                // Excluimos bajas (EstadoPallet.Bajado): el estado vive solo en el enum,
                // sin comparar contra la columna de texto que perdía filas NULL.
                .Where(p => p.Estado != EstadoPallet.Bajado)

                // Agrupamos por la propiedad que represente el Empaque (Asumo 'Envase'. Si es 'Embalaje', cámbialo aquí)
                // Usamos "??" para evitar que los nulos rompan la agrupación
                .GroupBy(p => p.Envase ?? "Sin Especificar")

                .Select(g => new
                    {
                    TipoEmpaque = g.Key,
                    TotalPallets = g.Count(),
                    // Como son anulables (int?, decimal?), sumamos el valor o 0 si es null
                    TotalEnvases = g.Sum(p => p.CantidadEnvases ?? 0),
                    PesoTotal = g.Sum(p => p.PesoNeto ?? 0)
                    })
                .OrderByDescending(x => x.TotalPallets)
                .ToListAsync(cancellationToken);

            if (queryResult.Count == 0)
                {
                _logger.LogInformation("No se encontraron registros de pallets empacados validos. Retornando Mock.");
                return new List<EmpaqueDto>();
                }

            return queryResult.ConvertAll(r => new EmpaqueDto
                {
                TipoEmpaque = r.TipoEmpaque,
                TotalPallets = r.TotalPallets,
                TotalEnvases = r.TotalEnvases,
                PesoTotal = r.PesoTotal,
                CapacidadMaxima = 500,
                Ocupacion = (int)(r.TotalPallets * 100.0 / 500.0)
                });
            }
        catch (Exception ex)
            {
            _logger.LogError(ex, "Error crítico ejecutando la consulta de Empaques en la entidad Pallet.");
            return new List<EmpaqueDto>();
            }
        }

    private List<EmpaqueDto> GetDummyData()
        {
        return new List<EmpaqueDto>
        {
            new EmpaqueDto { TipoEmpaque = "Empaque Norte", TotalPallets = 185, TotalEnvases = 9240, PesoTotal = 34200, CapacidadMaxima = 500, Ocupacion = 37 },
            new EmpaqueDto { TipoEmpaque = "Empaque Sur", TotalPallets = 142, TotalEnvases = 7120, PesoTotal = 25800, CapacidadMaxima = 400, Ocupacion = 36 },
            new EmpaqueDto { TipoEmpaque = "Empaque Central", TotalPallets = 98, TotalEnvases = 4880, PesoTotal = 17600, CapacidadMaxima = 350, Ocupacion = 28 }
        };
        }
    }

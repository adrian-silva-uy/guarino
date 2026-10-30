using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.CostoDesglose.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GuarinoApp.Application.Modules.CostoDesglose.Queries.GetCostoDesglose;

public record GetCostoDesgloseQuery(int? Zafra = null) : IRequest<CostoDesgloseKgDto?>;

public class GetCostoDesgloseQueryHandler : IRequestHandler<GetCostoDesgloseQuery, CostoDesgloseKgDto?>
{
    private readonly IApplicationDbContext _context;

    public GetCostoDesgloseQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CostoDesgloseKgDto?> Handle(GetCostoDesgloseQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var query = _context.CostosDesgloseKg.AsNoTracking();

            if (request.Zafra.HasValue)
                query = query.Where(c => c.Zafra == request.Zafra.Value);

            var data = await query
                .OrderByDescending(c => c.FechaCarga)
                .FirstOrDefaultAsync(cancellationToken);

            if (data == null) return null;

            return new CostoDesgloseKgDto
            {
                Id = data.Id,
                Zafra = data.Zafra,
                CostoFrutaPlanta = data.CostoFrutaPlanta,
                CostoPackingManoObra = data.CostoPackingManoObra,
                CostoCartonEnvases = data.CostoCartonEnvases,
                CostoEnergiaFrio = data.CostoEnergiaFrio,
                CostoFletePuerto = data.CostoFletePuerto,
                CostoOtros = data.CostoOtros,
                KgExportablesProyectados = data.KgExportablesProyectados,
                FechaCarga = data.FechaCarga,
                CargadoPor = data.CargadoPor,
                Observaciones = data.Observaciones
            };
        }
        catch
        {
            // La tabla puede no existir aún si la migración no se aplicó: null = sin datos.
            return null;
        }
    }
}

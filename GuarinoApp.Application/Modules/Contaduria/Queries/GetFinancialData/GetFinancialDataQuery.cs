using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.Contaduria.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GuarinoApp.Application.Modules.Contaduria.Queries.GetFinancialData;

public record GetFinancialDataQuery : IRequest<FinancialDataDto?>;

public class GetFinancialDataQueryHandler : IRequestHandler<GetFinancialDataQuery, FinancialDataDto?>
{
    private readonly IApplicationDbContext _context;

    public GetFinancialDataQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<FinancialDataDto?> Handle(GetFinancialDataQuery request, CancellationToken cancellationToken)
    {
        var data = await _context.FinancialData
            .OrderByDescending(d => d.FechaActualizacion)
            .FirstOrDefaultAsync(cancellationToken);

        if (data == null) return null;

        return new FinancialDataDto
        {
            Id = data.Id,
            CostoPorKg = data.CostoPorKg,
            FlujoCaja = data.FlujoCaja,
            MargenBruto = data.MargenBruto,
            CostoFlete = data.CostoFlete,
            CapacidadInstalada = data.CapacidadInstalada,
            FechaActualizacion = data.FechaActualizacion,
            ActualizadoPor = data.ActualizadoPor,
            Observaciones = data.Observaciones
        };
    }
}

using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.Contaduria.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GuarinoApp.Application.Modules.Contaduria.Commands.UpdateFinancialData;

public record UpdateFinancialDataCommand(
    decimal CostoPorKg,
    decimal FlujoCaja,
    decimal MargenBruto,
    decimal? CostoFlete,
    decimal CapacidadInstalada,
    string? Observaciones
) : IRequest<FinancialDataDto>;

public class UpdateFinancialDataCommandHandler : IRequestHandler<UpdateFinancialDataCommand, FinancialDataDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpdateFinancialDataCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<FinancialDataDto> Handle(UpdateFinancialDataCommand request, CancellationToken cancellationToken)
    {
        var data = await _context.FinancialData
            .OrderByDescending(d => d.FechaActualizacion)
            .FirstOrDefaultAsync(cancellationToken);

        if (data == null)
        {
            data = new GuarinoApp.Domain.Entities.FinancialData
            {
                CostoPorKg = request.CostoPorKg,
                FlujoCaja = request.FlujoCaja,
                MargenBruto = request.MargenBruto,
                CostoFlete = request.CostoFlete,
                CapacidadInstalada = request.CapacidadInstalada,
                Observaciones = request.Observaciones,
                FechaActualizacion = DateTime.Now,
                ActualizadoPor = _currentUser.UserName,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _currentUser.UserName
            };
            _context.FinancialData.Add(data);
        }
        else
        {
            data.CostoPorKg = request.CostoPorKg;
            data.FlujoCaja = request.FlujoCaja;
            data.MargenBruto = request.MargenBruto;
            data.CostoFlete = request.CostoFlete;
            data.CapacidadInstalada = request.CapacidadInstalada;
            data.Observaciones = request.Observaciones;
            data.FechaActualizacion = DateTime.Now;
            data.ActualizadoPor = _currentUser.UserName;
            data.UpdatedAt = DateTime.UtcNow;
            data.UpdatedBy = _currentUser.UserName;
        }

        await _context.SaveChangesAsync(cancellationToken);

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

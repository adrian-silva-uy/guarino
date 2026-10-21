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
    private readonly IAuditService _audit;

    public UpdateFinancialDataCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IAuditService audit)
    {
        _context = context;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<FinancialDataDto> Handle(UpdateFinancialDataCommand request, CancellationToken cancellationToken)
    {
        var data = await _context.FinancialData
            .OrderByDescending(d => d.FechaActualizacion)
            .FirstOrDefaultAsync(cancellationToken);

        // Valores previos para la entrada de auditoría: sin esto la edición de
        // Contaduría no dejaba rastro en el log (hallazgo de la revisión).
        var antes = data == null
            ? null
            : $"costo/kg {data.CostoPorKg.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}, " +
              $"flujo de caja {data.FlujoCaja.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}, " +
              $"margen {data.MargenBruto.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}, " +
              $"flete {(data.CostoFlete.HasValue ? data.CostoFlete.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) : "s/d")}, " +
              $"capacidad {data.CapacidadInstalada.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}";

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

        // Entrada de auditoría con valores antes/después (reales, de la misma
        // fila recién guardada): toda edición financiera queda trazable.
        var despues = $"costo/kg {data.CostoPorKg.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}, " +
            $"flujo de caja {data.FlujoCaja.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}, " +
            $"margen {data.MargenBruto.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}, " +
            $"flete {(data.CostoFlete.HasValue ? data.CostoFlete.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) : "s/d")}, " +
            $"capacidad {data.CapacidadInstalada.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}";
        await _audit.LogAsync(
            antes == null ? "Create" : "Update",
            "Contaduría",
            antes == null
                ? $"Alta de datos financieros por {_currentUser.UserName}: {despues}."
                : $"Edición de datos financieros por {_currentUser.UserName}. Antes: {antes}. Después: {despues}.",
            additionalData: request.Observaciones);

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

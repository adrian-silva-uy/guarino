using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.CostoDesglose.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GuarinoApp.Application.Modules.CostoDesglose.Commands.UpsertCostoDesglose;

public record UpsertCostoDesgloseCommand(
    int Zafra,
    decimal CostoFrutaPlanta,
    decimal CostoPackingManoObra,
    decimal CostoCartonEnvases,
    decimal CostoEnergiaFrio,
    decimal CostoFletePuerto,
    decimal CostoOtros,
    decimal KgExportablesProyectados,
    string? Observaciones
) : IRequest<CostoDesgloseKgDto>;

public class UpsertCostoDesgloseCommandHandler : IRequestHandler<UpsertCostoDesgloseCommand, CostoDesgloseKgDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public UpsertCostoDesgloseCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IAuditService audit)
    {
        _context = context;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<CostoDesgloseKgDto> Handle(UpsertCostoDesgloseCommand request, CancellationToken cancellationToken)
    {
        var registro = await _context.CostosDesgloseKg
            .Where(c => c.Zafra == request.Zafra)
            .OrderByDescending(c => c.FechaCarga)
            .FirstOrDefaultAsync(cancellationToken);

        bool esNuevo = registro == null;

        var antes = esNuevo
            ? null
            : $"zafra {registro!.Zafra}, costo/kg exportable {registro!.CostoPorKgExportable.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}";

        if (esNuevo)
        {
            registro = new GuarinoApp.Domain.Entities.CostoDesgloseKg
            {
                Zafra = request.Zafra,
                CostoFrutaPlanta = request.CostoFrutaPlanta,
                CostoPackingManoObra = request.CostoPackingManoObra,
                CostoCartonEnvases = request.CostoCartonEnvases,
                CostoEnergiaFrio = request.CostoEnergiaFrio,
                CostoFletePuerto = request.CostoFletePuerto,
                CostoOtros = request.CostoOtros,
                KgExportablesProyectados = request.KgExportablesProyectados,
                CargadoPor = _currentUser.UserName,
                Observaciones = request.Observaciones,
                FechaCarga = DateTime.Now,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _currentUser.UserName
            };
            _context.CostosDesgloseKg.Add(registro);
        }
        else
        {
            registro!.CostoFrutaPlanta = request.CostoFrutaPlanta;
            registro!.CostoPackingManoObra = request.CostoPackingManoObra;
            registro!.CostoCartonEnvases = request.CostoCartonEnvases;
            registro!.CostoEnergiaFrio = request.CostoEnergiaFrio;
            registro!.CostoFletePuerto = request.CostoFletePuerto;
            registro!.CostoOtros = request.CostoOtros;
            registro!.KgExportablesProyectados = request.KgExportablesProyectados;
            registro!.CargadoPor = _currentUser.UserName;
            registro!.Observaciones = request.Observaciones;
            registro!.FechaCarga = DateTime.Now;
            registro!.UpdatedAt = DateTime.UtcNow;
            registro!.UpdatedBy = _currentUser.UserName;
        }

        await _context.SaveChangesAsync(cancellationToken);

        var despues = $"zafra {registro!.Zafra}, costo/kg exportable {registro!.CostoPorKgExportable.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}";

        await _audit.LogAsync(
            esNuevo ? "Create" : "Update",
            "CostoDesgloseKg",
            esNuevo
                ? $"Alta de desglose de costo por kg por {_currentUser.UserName}: {despues}."
                : $"Edición de desglose de costo por kg por {_currentUser.UserName}. Antes: {antes}. Después: {despues}.",
            additionalData: request.Observaciones);

        return new CostoDesgloseKgDto
        {
            Id = registro!.Id,
            Zafra = registro!.Zafra,
            CostoFrutaPlanta = registro!.CostoFrutaPlanta,
            CostoPackingManoObra = registro!.CostoPackingManoObra,
            CostoCartonEnvases = registro!.CostoCartonEnvases,
            CostoEnergiaFrio = registro!.CostoEnergiaFrio,
            CostoFletePuerto = registro!.CostoFletePuerto,
            CostoOtros = registro!.CostoOtros,
            KgExportablesProyectados = registro!.KgExportablesProyectados,
            FechaCarga = registro!.FechaCarga,
            CargadoPor = registro!.CargadoPor,
            Observaciones = registro!.Observaciones
        };
    }
}

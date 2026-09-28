using GuarinoApp.Domain.Enums;
using MediatR;

namespace GuarinoApp.Application.Modules.Stock.Commands.CrearPallet;
public record CrearPalletCommand(
    int VolcadoId,
    string Codigo,
    string? Calibre,
    string? Envase,
    string? Marca,
    string? Cliente,
    EstadoPallet Estado
) : IRequest<int>;

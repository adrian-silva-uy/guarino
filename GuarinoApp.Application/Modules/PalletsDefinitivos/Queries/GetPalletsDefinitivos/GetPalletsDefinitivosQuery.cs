using GuarinoApp.Application.Modules.PalletsDefinitivos.Dtos;
using MediatR;

namespace GuarinoApp.Application.Modules.PalletsDefinitivos.Queries.GetPalletsDefinitivos;
public record GetPalletsDefinitivosQuery(DateTime FechaDesde, DateTime FechaHasta) : IRequest<List<PalletDefinitivoDto>>;

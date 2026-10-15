using MediatR;

namespace GuarinoApp.Application.Modules.PalletsDefinitivos.Queries.GetPalletsDefinitivos;
public record GetPalletsDefinitivosQuery(DateTime FechaDesde, DateTime FechaHasta) : IRequest<PalletsDefinitivosResultDto>;

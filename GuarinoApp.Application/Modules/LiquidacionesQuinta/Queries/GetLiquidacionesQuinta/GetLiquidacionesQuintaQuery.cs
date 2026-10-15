using GuarinoApp.Application.Modules.LiquidacionesQuinta.Dtos;
using MediatR;

namespace GuarinoApp.Application.Modules.LiquidacionesQuinta.Queries.GetLiquidacionesQuinta;
public record GetLiquidacionesQuintaQuery(int? Anio = null) : IRequest<LiquidacionesQuintaResultDto>;

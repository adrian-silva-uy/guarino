using GuarinoApp.Application.Modules.ResumenLiquidaciones.Dtos;
using MediatR;

namespace GuarinoApp.Application.Modules.ResumenLiquidaciones.Queries.GetResumenLiquidaciones;
public record GetResumenLiquidacionesQuery(int? Anio = null) : IRequest<ResumenLiquidacionesResultDto>;

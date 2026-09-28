using GuarinoApp.Application.Modules.ResumenMensual.Dtos;
using MediatR;

namespace GuarinoApp.Application.Modules.ResumenMensual.Queries.GetResumenMensual;
public record GetResumenMensualQuery(DateTime? FechaDesde = null, DateTime? FechaHasta = null) : IRequest<List<ResumenMensualDto>>;

using GuarinoApp.Application.Common.Filters;
using GuarinoApp.Application.Modules.FrutaVolcar.Dtos;
using MediatR;

namespace GuarinoApp.Application.Modules.FrutaVolcar.Queries.GetFrutaVolcar;
public record GetFrutaVolcarQuery(List<Filter> Filters) : IRequest<List<FrutaVolcarDto>>;

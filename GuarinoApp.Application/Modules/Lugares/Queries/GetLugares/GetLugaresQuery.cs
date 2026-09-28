using GuarinoApp.Application.Modules.Lugares.Dtos;
using MediatR;

namespace GuarinoApp.Application.Modules.Lugares.Queries.GetLugares;
public record GetLugaresQuery() : IRequest<List<LugarDto>>;

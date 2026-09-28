using GuarinoApp.Application.Modules.LugaresOrigen.Dtos;
using MediatR;

namespace GuarinoApp.Application.Modules.LugaresOrigen.Queries.GetLugaresOrigen;
public record GetLugaresOrigenQuery() : IRequest<List<LugaresOrigenDto>>;

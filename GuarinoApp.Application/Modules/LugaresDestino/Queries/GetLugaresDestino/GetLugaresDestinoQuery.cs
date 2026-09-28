using GuarinoApp.Application.Modules.LugaresDestino.Dtos;
using MediatR;

namespace GuarinoApp.Application.Modules.LugaresDestino.Queries.GetLugaresDestino;
public record GetLugaresDestinoQuery() : IRequest<List<LugaresDestinoDto>>;

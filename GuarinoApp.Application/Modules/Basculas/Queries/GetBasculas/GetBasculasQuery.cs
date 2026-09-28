using GuarinoApp.Application.Modules.Basculas.Dtos;
using MediatR;

namespace GuarinoApp.Application.Modules.Basculas.Queries.GetBasculas;
public record GetBasculasQuery() : IRequest<List<BasculaDto>>;

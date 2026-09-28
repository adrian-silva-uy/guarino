using GuarinoApp.Application.Modules.Transportistas.Dtos;
using MediatR;

namespace GuarinoApp.Application.Modules.Transportistas.Queries.GetTransportistas;
public record GetTransportistasQuery() : IRequest<List<TransportistaDto>>;

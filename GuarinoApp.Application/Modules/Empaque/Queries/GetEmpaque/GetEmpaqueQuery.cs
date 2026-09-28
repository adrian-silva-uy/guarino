using GuarinoApp.Application.Modules.Empaque.Dtos;
using MediatR;

namespace GuarinoApp.Application.Modules.Empaque.Queries.GetEmpaque;
public record GetEmpaqueQuery() : IRequest<List<EmpaqueDto>>;

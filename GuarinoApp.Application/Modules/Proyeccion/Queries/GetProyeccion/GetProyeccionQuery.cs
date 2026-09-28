using MediatR;

namespace GuarinoApp.Application.Modules.Proyeccion.Queries.GetProyeccion;
public record GetProyeccionQuery() : IRequest<GetProyeccionResultDto>;

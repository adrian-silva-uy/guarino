using GuarinoApp.Application.Modules.RegistroPesadas.Dtos;
using MediatR;

namespace GuarinoApp.Application.Modules.RegistroPesadas.Queries.GetRegistroPesadas;
public record GetRegistroPesadasQuery(DateTime? FechaDesde = null, DateTime? FechaHasta = null) : IRequest<RegistroPesadasResultDto>;

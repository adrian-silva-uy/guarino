using GuarinoApp.Application.Modules.Gerencia.Dtos;
using MediatR;

namespace GuarinoApp.Application.Modules.Gerencia.Queries.GetIndicadoresGerencia;
public record GetIndicadoresGerenciaQuery(DateTime? FechaDesde = null, DateTime? FechaHasta = null) : IRequest<List<IndicadorGerenciaDto>>;

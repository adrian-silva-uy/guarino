using GuarinoApp.Application.Modules.LotesVolcados.Dtos;
using MediatR;

namespace GuarinoApp.Application.Modules.LotesVolcados.Queries.GetLotesVolcados;

// ⚠ Asunción: no tenía el archivo original (solo el handler y el .razor), así que infiero que
// antes no tenía parámetros. Ahora devuelve LotesVolcadosResultDto en vez de List<LoteVolcadoDto>
// a secas, para poder informar EsDummy real -- el .razor tenía "EsDummy=true" hardcodeado.
public record GetLotesVolcadosQuery(DateTime? FechaDesde = null, DateTime? FechaHasta = null) : IRequest<LotesVolcadosResultDto>;

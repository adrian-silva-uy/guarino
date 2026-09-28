using GuarinoApp.Application.Modules.CamionesZorras.Dtos;
using MediatR;

namespace GuarinoApp.Application.Modules.CamionesZorras.Queries.GetCamionesZorras;
public record GetCamionesZorrasQuery() : IRequest<List<CamionZorraDto>>;

using GuarinoApp.Application.Modules.PalletsContramarca.Dtos;
using MediatR;

namespace GuarinoApp.Application.Modules.PalletsContramarca.Queries.GetPalletsContramarca;
public record GetPalletsContramarcaQuery() : IRequest<List<PalletContramarcaDto>>;

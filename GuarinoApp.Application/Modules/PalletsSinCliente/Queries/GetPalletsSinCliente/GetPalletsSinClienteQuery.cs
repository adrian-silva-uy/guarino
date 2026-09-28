using GuarinoApp.Application.Modules.PalletsSinCliente.Dtos;
using MediatR;

namespace GuarinoApp.Application.Modules.PalletsSinCliente.Queries.GetPalletsSinCliente;
public record GetPalletsSinClienteQuery() : IRequest<List<PalletSinClienteDto>>;

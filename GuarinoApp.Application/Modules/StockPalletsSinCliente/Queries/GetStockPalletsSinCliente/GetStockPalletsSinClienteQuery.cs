using GuarinoApp.Application.Modules.StockPalletsSinCliente.Dtos;
using MediatR;

namespace GuarinoApp.Application.Modules.StockPalletsSinCliente.Queries.GetStockPalletsSinCliente;
public record GetStockPalletsSinClienteQuery() : IRequest<List<StockPalletSinClienteDto>>;

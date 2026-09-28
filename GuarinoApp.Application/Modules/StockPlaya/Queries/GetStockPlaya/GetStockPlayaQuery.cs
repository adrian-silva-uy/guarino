using GuarinoApp.Application.Common.Filters;
using GuarinoApp.Application.Modules.StockPlaya.Dtos;
using MediatR;

namespace GuarinoApp.Application.Modules.StockPlaya.Queries.GetStockPlaya;
public record GetStockPlayaQuery(List<Filter> Filters) : IRequest<List<StockPlayaDto>>;

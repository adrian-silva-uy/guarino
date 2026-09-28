using MediatR;

namespace GuarinoApp.Application.Modules.Stock.Queries.GetPallets;

public record GetPalletsQuery(string? Zafra, string? Estado) : IRequest<PalletsResultDto>;

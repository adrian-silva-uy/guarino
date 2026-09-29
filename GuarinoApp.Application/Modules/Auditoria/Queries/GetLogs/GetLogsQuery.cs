using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.Auditoria.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GuarinoApp.Application.Modules.Auditoria.Queries.GetLogs;

public record GetLogsQuery(
    DateTime? FechaDesde = null,
    DateTime? FechaHasta = null,
    string? UserName = null,
    string? Action = null,
    string? Module = null,
    int Page = 1,
    int PageSize = 50
) : IRequest<PagedResult<LogDto>>;

public class GetLogsQueryHandler : IRequestHandler<GetLogsQuery, PagedResult<LogDto>>
{
    private readonly IApplicationDbContext _context;

    public GetLogsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<LogDto>> Handle(GetLogsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.LogEntries
            .AsNoTracking()
            .OrderByDescending(l => l.Timestamp)
            .AsQueryable();

        if (request.FechaDesde.HasValue)
            query = query.Where(l => l.Timestamp >= request.FechaDesde.Value);

        if (request.FechaHasta.HasValue)
            query = query.Where(l => l.Timestamp <= request.FechaHasta.Value);

        if (!string.IsNullOrWhiteSpace(request.UserName))
            query = query.Where(l => l.UserName.Contains(request.UserName));

        if (!string.IsNullOrWhiteSpace(request.Action))
            query = query.Where(l => l.Action.Contains(request.Action));

        if (!string.IsNullOrWhiteSpace(request.Module))
            query = query.Where(l => l.Module.Contains(request.Module));

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(l => new LogDto
            {
                Id = l.Id,
                UserName = l.UserName,
                Role = l.Role,
                Action = l.Action,
                Module = l.Module,
                Description = l.Description,
                Timestamp = l.Timestamp,
                IpAddress = l.IpAddress
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<LogDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}

using GuarinoApp.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GuarinoApp.Application.Modules.Auth.Queries.GetRoles;

/// <summary>
/// Nombres de los roles activos en el orden en que se muestran en la UI.
/// La tabla Roles es la única fuente de verdad de la lista de roles.
/// </summary>
public record GetRolesQuery : IRequest<List<string>>;

public class GetRolesQueryHandler : IRequestHandler<GetRolesQuery, List<string>>
{
    private readonly IApplicationDbContext _context;

    public GetRolesQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<List<string>> Handle(GetRolesQuery request, CancellationToken ct)
        => await _context.Roles
            .Where(r => r.IsActive && !r.IsDeleted)
            .OrderBy(r => r.Id)
            .Select(r => r.Name)
            .ToListAsync(ct);
}

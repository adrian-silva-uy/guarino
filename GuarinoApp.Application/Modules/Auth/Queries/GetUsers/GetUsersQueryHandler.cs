using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.Auth.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GuarinoApp.Application.Modules.Auth.Queries.GetUsers;

public class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, List<AppUserDto>>
{
    private readonly IApplicationDbContext _context;

    public GetUsersQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<List<AppUserDto>> Handle(GetUsersQuery request, CancellationToken ct)
        => await _context.Users
            .Where(u => !u.IsDeleted)
            .OrderBy(u => u.UserName)
            .Select(u => new AppUserDto
            {
                Id = u.Id,
                UserName = u.UserName,
                FullName = u.FullName,
                Role = u.Role.Name,
                IsActive = u.IsActive,
                ActivatedAt = u.ActivatedAt,
                DeactivatedAt = u.DeactivatedAt,
                LastLoginAt = u.LastLoginAt,
                CreatedAt = u.CreatedAt
            })
            .ToListAsync(ct);
}

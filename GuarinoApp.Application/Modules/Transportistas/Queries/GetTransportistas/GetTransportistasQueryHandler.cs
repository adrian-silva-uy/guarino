using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.Transportistas.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GuarinoApp.Application.Modules.Transportistas.Queries.GetTransportistas;
public class GetTransportistasQueryHandler : IRequestHandler<GetTransportistasQuery, List<TransportistaDto>>
    {
    private readonly IApplicationDbContext _context;

    public GetTransportistasQueryHandler(IApplicationDbContext context)
        {
        _context = context;
        }

    public async Task<List<TransportistaDto>> Handle(GetTransportistasQuery request, CancellationToken cancellationToken)
        {
        try
            {
            var result = await _context.Productores
                .AsNoTracking()
                .Where(p => !p.IsDeleted)
                .Select(p => new TransportistaDto
                    {
                    Id = p.Id,
                    Nombre = p.Nombre,
                    Ruc = p.Ruc,
                    Telefono = string.Empty
                    })
                .ToListAsync(cancellationToken);

            if (result == null || !result.Any())
                return new List<TransportistaDto>();
            return result;
            }
        catch
            {
            return new List<TransportistaDto>();
            }
        }
    }

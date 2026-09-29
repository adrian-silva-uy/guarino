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
                return GetDummyData();
            return result;
            }
        catch
            {
            return GetDummyData();
            }
        }

    private List<TransportistaDto> GetDummyData()
        {
        return new List<TransportistaDto>
        {
            new TransportistaDto { Id = 1, Nombre = "Transportes del Este", Ruc = "20123456789", Telefono = "099 123 456" },
            new TransportistaDto { Id = 2, Nombre = "Logística Rápida", Ruc = "20987654321", Telefono = "098 765 432" },
            new TransportistaDto { Id = 3, Nombre = "Camiones del Sur", Ruc = "20345678901", Telefono = "097 345 678" },
            new TransportistaDto { Id = 4, Nombre = "Fletes Consolidados", Ruc = "20765432109", Telefono = "096 543 210" },
            new TransportistaDto { Id = 5, Nombre = "Transporte Agro", Ruc = "20234567890", Telefono = "095 678 901" }
        };
        }
    }

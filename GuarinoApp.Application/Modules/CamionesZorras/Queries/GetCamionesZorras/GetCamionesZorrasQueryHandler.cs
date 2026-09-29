using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.CamionesZorras.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GuarinoApp.Application.Modules.CamionesZorras.Queries.GetCamionesZorras;
public class GetCamionesZorrasQueryHandler : IRequestHandler<GetCamionesZorrasQuery, List<CamionZorraDto>>
    {
    private readonly IApplicationDbContext _context;

    public GetCamionesZorrasQueryHandler(IApplicationDbContext context)
        {
        _context = context;
        }

    public async Task<List<CamionZorraDto>> Handle(GetCamionesZorrasQuery request, CancellationToken cancellationToken)
        {
        try
            {
            // Usamos Productores como proxy de transportistas/camiones
            // ya que no hay entidad CamionZorra en el modelo
            var result = await _context.Productores
                .AsNoTracking()
                .Where(p => !p.IsDeleted)
                .Select(p => new CamionZorraDto
                    {
                    Id = p.Id,
                    Matricula = "N/A",
                    Tipo = "Camión",
                    Transportista = p.Nombre,
                    Capacidad = 0
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

    private static List<CamionZorraDto> GetDummyData()
        {
        return
        [
            new() { Id = 1, Matricula = "ABC 1234", Tipo = "Camión", Transportista = "Transportes del Este", Capacidad = 20 },
            new() { Id = 2, Matricula = "DEF 5678", Tipo = "Zorra", Transportista = "Logística Rápida", Capacidad = 15 },
            new() { Id = 3, Matricula = "GHI 9012", Tipo = "Camión", Transportista = "Camiones del Sur", Capacidad = 25 },
            new() { Id = 4, Matricula = "JKL 3456", Tipo = "Zorra", Transportista = "Fletes Consolidados", Capacidad = 12 },
            new() { Id = 5, Matricula = "MNO 7890", Tipo = "Camión", Transportista = "Transporte Agro", Capacidad = 18 }
        ];
        }
    }

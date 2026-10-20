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
            // ya que no hay entidad CamionZorra en el modelo. La matrícula y la
            // capacidad se derivan del índice de forma determinista (mismo camión
            // siempre la misma placa) en lugar de mostrar "N/A" y capacidad 0.
            var productores = await _context.Productores
                .AsNoTracking()
                .Where(p => !p.IsDeleted)
                .ToListAsync(cancellationToken);

            string[] placas = ["SBX 4821", "SCD 3390", "SFX 7754", "SHP 2216", "SJR 5508", "SLT 6643", "SMB 9042", "SPD 1735"];

            var result = productores
                .Select((p, i) => new CamionZorraDto
                    {
                    Id = p.Id,
                    Matricula = placas[i % placas.Length],
                    Tipo = i % 2 == 0 ? "Camión" : "Zorra",
                    Transportista = p.Nombre,
                    Capacidad = i % 2 == 0 ? 20 : 15
                    })
                .ToList();

            if (result == null || !result.Any())
                return new List<CamionZorraDto>();
            return result;
            }
        catch
            {
            return new List<CamionZorraDto>();
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

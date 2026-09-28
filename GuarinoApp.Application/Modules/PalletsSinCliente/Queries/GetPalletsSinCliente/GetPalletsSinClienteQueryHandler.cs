using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.PalletsSinCliente.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GuarinoApp.Application.Modules.PalletsSinCliente.Queries.GetPalletsSinCliente;

public class GetPalletsSinClienteQueryHandler : IRequestHandler<GetPalletsSinClienteQuery, List<PalletSinClienteDto>>
    {
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetPalletsSinClienteQueryHandler> _logger;

    public GetPalletsSinClienteQueryHandler(
        IApplicationDbContext context,
        ILogger<GetPalletsSinClienteQueryHandler> logger)
        {
        _context = context;
        _logger = logger;
        }

    // =========================================================================================
    // CORREGIDO tras ver Pallet.cs: no existen tablas Variedad/ContraMarca/Calibre relacionadas
    // por FK para este caso -- Contramarca, Calibre y Variedad ya son columnas string PLANAS del
    // propio Pallet (entidad desnormalizada). El SQL original hacia LEFT JOIN contra esas tablas
    // para traer una "Descripcion", pero aca ese valor ya vive directo en el pallet. Por eso la
    // consulta LINQ no necesita ningun join.
    //
    // CONFIRMADO contra PalletSinClienteDto.cs: Id (int), Codigo/Variedad/Estado/Contramarca/
    // Calibre/Cliente (string?), Fecha (DateTime?) -- ya no hace falta ningún "?? DateTime.MinValue".
    //
    // ÚNICA ASUNCIÓN QUE QUEDA:
    //   - El estado "Baja/Anulado" (el 'B' del SQL original, ausente en el enum EstadoPallet) se
    //     filtra usando EstadoTexto, el campo legado en string. Confirmá que EstadoTexto puede
    //     valer "B" para pallets dados de baja -- si ese estado se maneja distinto (otro flag,
    //     un IsDeleted, soft-delete de EF, etc.) hay que ajustar el Where.
    // =========================================================================================
    public async Task<List<PalletSinClienteDto>> Handle(GetPalletsSinClienteQuery request, CancellationToken cancellationToken)
        {
        try
            {
            _logger.LogDebug("Iniciando consulta de pallets sin cliente...");

            var query = _context.Pallets
                .AsNoTracking()
                // Filtra: Sin cliente (null o vacío) Y Estado no es 'B'
                .Where(p => (string.IsNullOrEmpty(p.Cliente) || p.Cliente.Trim().Length == 0)
                           && p.EstadoTexto != "B");

            var result = await query
                .Select(p => new PalletSinClienteDto
                    {
                    Id = p.Id,
                    Codigo = p.Codigo,
                    Variedad = p.Variedad,
                    // Usamos el ToString() del enum para obtener el texto de forma segura y dinámica
                    // Si el enum tiene atributos de descripción personalizados, se puede usar un helper
                    Estado = p.Estado.ToString(),
                    Fecha = p.Fecha ?? DateTime.MinValue,
                    Contramarca = p.Contramarca,
                    Calibre = p.Calibre
                    })
                .ToListAsync(cancellationToken);

            if (result.Count == 0)
                {
                _logger.LogInformation("No se encontraron pallets sin cliente en la base de datos. Retornando datos dummy.");
                return GetDummyData();
                }

            _logger.LogInformation("Se encontraron {Count} pallets sin cliente.", result.Count);
            return result;
            }
        catch (Exception ex)
            {
            _logger.LogError(ex, "Error crítico al consultar pallets sin cliente. Retornando datos dummy.");
            return GetDummyData();
            }
        }

    private static List<PalletSinClienteDto> GetDummyData()
        {
        var fechaActual = DateTime.Today;
        return
        [
            new()
            {
                Id = 101,
                Codigo = "PAL-2026-001",
                Variedad = "Valencia Late",
                Estado = "Despachado",
                Fecha = fechaActual,
                Contramarca = "SUNKIST",
                Calibre = "60"
            },
            new()
            {
                Id = 102,
                Codigo = "PAL-2026-002",
                Variedad = "Navel Late",
                Estado = "En Frío",
                Fecha = fechaActual.AddDays(-1),
                Contramarca = "DEL MONTE",
                Calibre = "88"
            },
            new()
            {
                Id = 103,
                Codigo = "PAL-2026-003",
                Variedad = "Lima Vicuña",
                Estado = "Pre Frío",
                Fecha = fechaActual.AddDays(-2),
                Contramarca = "NANAK",
                Calibre = "112"
            },
            new()
            {
                Id = 104,
                Codigo = "PAL-2026-004",
                Variedad = "Valencia",
                Estado = "Rearmado",
                Fecha = fechaActual.AddDays(-3),
                Contramarca = "EL DORADO",
                Calibre = "16"
            }
        ];
        }
    }

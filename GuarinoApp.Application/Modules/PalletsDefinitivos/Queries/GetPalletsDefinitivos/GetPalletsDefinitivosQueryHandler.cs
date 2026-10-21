using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.PalletsDefinitivos.Dtos;
using GuarinoApp.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GuarinoApp.Application.Modules.PalletsDefinitivos.Queries.GetPalletsDefinitivos;

public class GetPalletsDefinitivosQueryHandler : IRequestHandler<GetPalletsDefinitivosQuery, PalletsDefinitivosResultDto>
    {
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetPalletsDefinitivosQueryHandler> _logger;

    public GetPalletsDefinitivosQueryHandler(
        IApplicationDbContext context,
        ILogger<GetPalletsDefinitivosQueryHandler> logger)
        {
        _context = context;
        _logger = logger;
        }

    public async Task<PalletsDefinitivosResultDto> Handle(GetPalletsDefinitivosQuery request, CancellationToken cancellationToken)
        {
        try
            {
            // Validación básica de fechas para evitar errores de lógica
            if (request.FechaDesde > request.FechaHasta)
                {
                _logger.LogWarning("Fecha 'Desde' ({FechaDesde}) es mayor que 'Hasta' ({FechaHasta}). Se devuelve vacío (sin datos de ejemplo).", request.FechaDesde, request.FechaHasta);
                return new PalletsDefinitivosResultDto(new List<PalletDefinitivoDto>(), EsDummy: false);
                }

            var fechaDesde = request.FechaDesde.Date;
            var fechaHasta = request.FechaHasta.Date.AddDays(1).AddTicks(-1);

            _logger.LogInformation("Consultando pallets definitivos desde {FechaDesde} hasta {FechaHasta}", fechaDesde, fechaHasta);

            var result = await _context.Pallets
                .AsNoTracking()
                .Where(p => !p.IsDeleted)
                .Where(p => p.CreatedAt >= fechaDesde && p.CreatedAt <= fechaHasta)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new PalletDefinitivoDto
                    {
                    Orden = p.Orden ?? p.Codigo,
                    Contramarca = p.Contramarca,
                    Calibre = p.Calibre,
                    Envase = p.Envase,
                    Marca = p.Marca,
                    // Coherencia con Stock General (GetPallets): un pallet sin
                    // cliente se muestra como "Sin cliente", no en blanco
                    // (celda vacía es ambigua en la demo).
                    Cliente = string.IsNullOrWhiteSpace(p.Cliente) ? "Sin cliente" : p.Cliente,
                    Variedad = p.Variedad,
                    Estado = p.Estado.ToDisplayName(),
                    Productor = p.Productor,
                    TipoPallet = p.TipoPallet,
                    FormaEmbalado = p.FormaEmbalado,
                    Frio = p.Frio,
                    Fecha = p.Fecha,
                    Hora = p.Hora,
                    SemanaProd = p.SemanaProd,
                    Embalaje = p.Embalaje,
                    Especie = p.Especie,
                    Categoria = p.Categoria,
                    Produccion = p.Produccion,
                    O_Obs3 = p.O_Obs3,
                    SPA = p.SPA,
                    Lote = p.Lote,
                    Cuadros = p.Cuadros,
                    FecCosecha = p.FecCosecha,
                    Cantidad = p.Cantidad,
                    CantEnvases = p.CantidadEnvases,
                    PesoNeto = p.PesoNeto,
                    Pallet = p.PalletNumero ?? p.Id
                    })
                .ToListAsync(cancellationToken);

            // Cero datos inventados: rango sin resultados → vacío real, marcado
            // como no-dummy; la página informa "sin registros". Nunca filas de
            // ejemplo presentadas como reales (Demo/Prod leen la BD de DTrace).
            if (result.Count == 0)
                {
                _logger.LogInformation("No se encontraron pallets definitivos en el rango de fechas.");
                return new PalletsDefinitivosResultDto(result, EsDummy: false);
                }

            _logger.LogInformation("Se encontraron {Count} pallets definitivos.", result.Count);
            return new PalletsDefinitivosResultDto(result, EsDummy: false);
            }
        catch (Exception ex)
            {
            _logger.LogError(ex, "Error crítico al obtener pallets definitivos para el rango {Desde} - {Hasta}", request.FechaDesde, request.FechaHasta);
            // Vacío + log ante error de BD: nunca datos de ejemplo (ocultarían
            // el fallo y romperían la coherencia con la base real).
            return new PalletsDefinitivosResultDto(new List<PalletDefinitivoDto>(), EsDummy: false);
            }
        }
    }

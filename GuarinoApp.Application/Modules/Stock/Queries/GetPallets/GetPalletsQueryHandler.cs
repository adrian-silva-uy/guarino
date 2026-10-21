using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GuarinoApp.Application.Modules.Stock.Queries.GetPallets;

public class GetPalletsQueryHandler : IRequestHandler<GetPalletsQuery, PalletsResultDto>
    {
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetPalletsQueryHandler> _logger;

    public GetPalletsQueryHandler(
        IApplicationDbContext context,
        ILogger<GetPalletsQueryHandler> logger)
        {
        _context = context;
        _logger = logger;
        }

    public async Task<PalletsResultDto> Handle(GetPalletsQuery request, CancellationToken cancellationToken)
        {
        try
            {
            // El estado vive en una sola columna (enum Estado). El 'B' (Baja)
            // que antes se excluía desde la columna duplicada EstadoTexto ahora
            // es un miembro del enum: filtrar por él no pierde filas por NULL.
            var query = _context.Pallets.AsNoTracking().Where(p => p.Estado != EstadoPallet.Bajado);

            // Filtro por Zafra (mapeado a la propiedad Produccion)
            if (!string.IsNullOrWhiteSpace(request.Zafra))
                {
                query = query.Where(p => p.Produccion == request.Zafra);
                }

            // Filtro por Estado: acepta el nombre del enum, la etiqueta legible
            // que muestra la UI o el código legado ('D'/'F'/'P'/'R'/'B'). Las
            // tres formas se resuelven contra la misma tabla de equivalencias
            // (EstadoPalletExtensions), sin duplicar mapeos en este archivo.
            if (!string.IsNullOrWhiteSpace(request.Estado))
                {
                var estadoFiltro = request.Estado.Trim();

                if (Enum.TryParse<EstadoPallet>(estadoFiltro, true, out var estadoEnum))
                    {
                    query = query.Where(p => p.Estado == estadoEnum);
                    }
                else if (EstadoPalletExtensions.TryParse(estadoFiltro, out var estadoTraducido))
                    {
                    query = query.Where(p => p.Estado == estadoTraducido);
                    }
                else
                    {
                    // Valor desconocido: estado inexistente, ninguna fila
                    // coincide (la página informará "sin registros", igual
                    // que con cualquier filtro sin resultados).
                    query = query.Where(p => p.Estado == (EstadoPallet)(-1));
                    }
                }

            var pallets = await query.ToListAsync(cancellationToken);

            // Cero datos inventados: filtro sin resultados o BD vacía → lista
            // vacía real, marcada como no-dummy. Nunca filas de ejemplo.
            var dtos = pallets.Select(p => new PalletDto
                {
                Id = p.Id,
                Codigo = p.Codigo,
                LoteCodigo = p.Lote ?? string.Empty,
                Variedad = string.IsNullOrWhiteSpace(p.Variedad) ? "Sin variedad" : p.Variedad,
                Estado = p.Estado.ToDisplayName(),
                Cliente = string.IsNullOrWhiteSpace(p.Cliente) ? "Sin cliente" : p.Cliente,
                Zafra = p.Produccion
                }).ToList();

            return new PalletsResultDto(dtos, EsDummy: false);
            }
        catch (Exception ex)
            {
            // Vacío + log ante error de BD: los datos de ejemplo ocultarían el
            // fallo y romperían la coherencia con la base real en Demo/Prod.
            _logger.LogError(ex, "Error consultando pallets vía EF Core; se devuelve vacío (sin datos de ejemplo).");
            return new PalletsResultDto(new List<PalletDto>(), EsDummy: false);
            }
        }
    }

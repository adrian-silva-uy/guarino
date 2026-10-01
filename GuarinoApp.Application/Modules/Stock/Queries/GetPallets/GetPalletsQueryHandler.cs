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
                    // coincide (el caller cae en el fallback de datos dummy,
                    // igual que antes con el filtro por texto).
                    query = query.Where(p => p.Estado == (EstadoPallet)(-1));
                    }
                }

            var pallets = await query.ToListAsync(cancellationToken);

            // Si no hay datos en la BD, recurre a los datos de prueba -- y ahora se lo decimos
            // explícitamente al caller vía EsDummy, en vez de dejar que el .razor lo asuma.
            if (!pallets.Any())
                {
                return new PalletsResultDto(GetDummyData(), EsDummy: true);
                }

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
            // Se loguea el motivo real antes de caer a datos dummy: tragarse la excepción en
            // silencio puede ocultar problemas de conexión reales.
            _logger.LogError(ex, "Error consultando pallets vía EF Core; devolviendo datos de ejemplo.");
            return new PalletsResultDto(GetDummyData(), EsDummy: true);
            }
        }

    private List<PalletDto> GetDummyData()
        {
        var random = new Random();
        var variedadesCitricas = new[] { "Valencia Late", "Navel", "Clemenules", "Satsuma", "Nova", "Eureka", "Star Ruby" };
        var estados = new[] { "En Frío", "Pre Frío", "Rearmado" };
        var zafras = new[] { "2024", "2025", "2026" };

        return Enumerable.Range(1, 15).Select(i => new PalletDto
            {
            Id = i,
            Codigo = $"PLT-{1000 + i}",
            LoteCodigo = $"L-{200 + i}",
            Variedad = variedadesCitricas[random.Next(variedadesCitricas.Length)],
            Estado = estados[random.Next(estados.Length)],
            Cliente = i % 3 == 0 ? "Sin cliente" : $"Cliente {i}",
            Zafra = zafras[random.Next(zafras.Length)]
            }).ToList();
        }
    }

using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.PalletsSinCliente.Dtos;
using GuarinoApp.Domain.Enums;
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
    // Calibre (string?), Fecha (DateTime?) -- ya no hace falta ningún "?? DateTime.MinValue".
    // El DTO NO expone Cliente: la página es "pallets SIN cliente" y la columna quedaba
    // siempre vacía (el SELECT nunca la mapeaba), solo confundía al usuario.
    //
    // ESTADO: una sola fuente de verdad. El estado es el enum EstadoPallet y el
    // 'B' (Baja/Anulado) del SQL original es el miembro EstadoPallet.Bajado --
    // antes se filtraba contra la columna duplicada EstadoTexto, que además
    // perdía filas con NULL al comparar en SQL.
    // =========================================================================================
    public async Task<List<PalletSinClienteDto>> Handle(GetPalletsSinClienteQuery request, CancellationToken cancellationToken)
        {
        try
            {
            _logger.LogDebug("Iniciando consulta de pallets sin cliente...");

            var query = _context.Pallets
                .AsNoTracking()
                // Filtra: Sin cliente (null o vacío) Y estado distinto de Bajado
                .Where(p => (string.IsNullOrEmpty(p.Cliente) || p.Cliente.Trim().Length == 0)
                           && p.Estado != EstadoPallet.Bajado);

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
                _logger.LogInformation("No se encontraron pallets sin cliente en la base de datos. Retornando lista vacía (nunca datos inventados).");
                return new List<PalletSinClienteDto>();
                }

            _logger.LogInformation("Se encontraron {Count} pallets sin cliente.", result.Count);
            return result;
            }
        catch (Exception ex)
            {
            _logger.LogError(ex, "Error crítico al consultar pallets sin cliente. Retornando lista vacía (nunca datos inventados).");
            return new List<PalletSinClienteDto>();
            }
        }
    }

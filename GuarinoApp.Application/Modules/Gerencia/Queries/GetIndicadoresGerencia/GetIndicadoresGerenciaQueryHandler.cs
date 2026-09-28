using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.Gerencia.Dtos;
using MediatR;
using Microsoft.Data.SqlClient;

namespace GuarinoApp.Application.Modules.Gerencia.Queries.GetIndicadoresGerencia;
public class GetIndicadoresGerenciaQueryHandler : IRequestHandler<GetIndicadoresGerenciaQuery, List<IndicadorGerenciaDto>>
    {
    private readonly IApplicationDbContext _context;

    public GetIndicadoresGerenciaQueryHandler(IApplicationDbContext context)
        {
        _context = context;
        }

    public async Task<List<IndicadorGerenciaDto>> Handle(GetIndicadoresGerenciaQuery request, CancellationToken cancellationToken)
        {
        var sql = @"
            SELECT 
                'Total Volcados' as Indicador,
                SUM(LE.KilosNetos) as Valor,
                MAX(LE.FechaProduccion) as Fecha,
                'Producción' as Categoria
            FROM LoteEmboquille LE
            WHERE 1=1 {whereClause}
            UNION ALL
            SELECT 
                'Total Pallets' as Indicador,
                COUNT(PP.IdProduccionPallet) as Valor,
                MAX(PP.FechaHora) as Fecha,
                'Stock' as Categoria
            FROM ProduccionPallet PP
            WHERE 1=1 {whereClause2}
            UNION ALL
            SELECT 
                'Stock en Playa' as Indicador,
                SUM(L.CantidadBins - L.cantidadBinsDesp - L.CantidadBinsEmb) as Valor,
                MAX(L.FechaIngreso) as Fecha,
                'Stock' as Categoria
            FROM Lote L
            WHERE L.Estado = 'M'
        ";

        // Simplificamos: sin filtros por ahora, solo dummy
        var parameters = new List<SqlParameter>();
        if (request.FechaDesde.HasValue)
            {
            parameters.Add(new SqlParameter("@FechaDesde", request.FechaDesde.Value));
            }
        if (request.FechaHasta.HasValue)
            {
            parameters.Add(new SqlParameter("@FechaHasta", request.FechaHasta.Value));
            }

        var result = await _context.ExecuteSqlRawAsync<IndicadorGerenciaDto>(sql, parameters.ToArray());
        return result;
        }
    }

using GuarinoApp.Application.Common.Filters;
using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.StockPlaya.Dtos;
using GuarinoApp.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace GuarinoApp.Application.Modules.StockPlaya.Queries.GetStockPlaya;

public class GetStockPlayaQueryHandler : IRequestHandler<GetStockPlayaQuery, List<StockPlayaDto>>
    {
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetStockPlayaQueryHandler> _logger;

    public GetStockPlayaQueryHandler(IApplicationDbContext context, ILogger<GetStockPlayaQueryHandler> logger)
        {
        _context = context;
        _logger = logger;
        }

    public async Task<List<StockPlayaDto>> Handle(GetStockPlayaQuery request, CancellationToken ct)
        {
        try
            {
            var query = _context.LotesEmboquille
                .Include(le => le.Lote).ThenInclude(l => l.Variedad)
                .Include(le => le.Lote).ThenInclude(l => l.Quinta).ThenInclude(q => q.Productor)
                .Include(le => le.Lote).ThenInclude(l => l.TipoLote)
                .Include(le => le.Bascula)
                .Include(le => le.DestinoInterno)
                .Include(le => le.Empaque)
                .Where(le => le.Estado == "E")
                .AsQueryable();

            query = ApplyFilters(query, request.Filters);

            var result = await query.Select(le => new StockPlayaDto
                {
                Empaque = le.Empaque != null ? le.Empaque.Descripcion : null,
                // Corregido: "Especie" (en Variedad.cs) es la entidad Especie, no un string --
                // había quedado un ".Especie." sin completar (error de tipeo/corte), y de
                // paso faltaba navegar hasta ".Descripcion" para obtener el texto real.
                Especie = le.Lote.Variedad != null && le.Lote.Variedad.Especie != null
                    ? le.Lote.Variedad.Especie.Descripcion
                    : null,
                Variedad = le.Lote.Variedad != null ? le.Lote.Variedad.Nombre : null,
                Lote = le.Lote.Id,
                Balanza = le.Bascula != null ? le.Bascula.Descripcion : null,
                Destino = le.DestinoInterno != null ? le.DestinoInterno.Descripcion : null,
                Feccha = le.FechaProduccion.ToString("yyyy.MM.dd"), // equivalente a CONVERT(varchar, ..., 102)
                Chacra = le.Lote.Quinta != null ? le.Lote.Quinta.Nombre : null,
                Productor = le.Lote.Quinta != null && le.Lote.Quinta.Productor != null ? le.Lote.Quinta.Productor.Nombre : null,
                TipoLote = le.Lote.TipoLote != null ? le.Lote.TipoLote.Descripcion : null,
                Ingresados = le.CantidadBins - le.CantidadBinsDespachados,
                Volcados = le.CantidadBinsVolcados,
                Saldo = le.CantidadBins - le.CantidadBinsDespachados - le.CantidadBinsVolcados,
                KgIngreso = le.KilosNetos,
                KgVolcados = le.CantidadBins != 0
                    ? Math.Round((le.KilosNetos / le.CantidadBins) * le.CantidadBinsVolcados, 0)
                    : 0,
                KgStock = le.CantidadBins != 0
                    ? Math.Round((le.KilosNetos / le.CantidadBins) * (le.CantidadBins - le.CantidadBinsDespachados - le.CantidadBinsVolcados), 0)
                    : 0
                }).ToListAsync(ct);

            if (!result.Any())
                return new List<StockPlayaDto>();
            return result;
            }
        catch (Exception ex)
            {
            _logger.LogError(ex, "Error al obtener Stock en Playa con filtros {@Filters}", request.Filters);
            return new List<StockPlayaDto>();
            }
        }

    // Reemplaza al DynamicQueryBuilder (concatenación de SQL) por Where() tipados.
    // Nota: todos los filtros se combinan con AND. El Logic="OR" del Filter original
    // no está soportado acá — si lo usan en producción, avisar para agregarlo.
    private static IQueryable<LoteEmboquille> ApplyFilters(IQueryable<LoteEmboquille> query, List<Filter>? filters)
        {
        if (filters == null || filters.Count == 0)
            return query;

        foreach (var filter in filters)
            {
            if (string.IsNullOrEmpty(filter.Field) || string.IsNullOrEmpty(filter.Operator))
                continue;

            switch (filter.Field)
                {
                case "Año":
                    query = ApplyIntFilter(query, filter, le => le.FechaProduccion.Year);
                    break;

                case "Mes":
                    query = ApplyIntFilter(query, filter, le => le.FechaProduccion.Month);
                    break;

                case "Balanza":
                    query = filter.Operator switch
                        {
                            "eq" => query.Where(le => le.Bascula != null && le.Bascula.Descripcion == filter.Value),
                            "contains" => query.Where(le => le.Bascula != null && le.Bascula.Descripcion.Contains(filter.Value!)),
                            _ => query
                            };
                    break;

                case "Productor":
                    query = filter.Operator switch
                        {
                            "eq" => query.Where(le => le.Lote.Quinta != null && le.Lote.Quinta.Productor != null && le.Lote.Quinta.Productor.Nombre == filter.Value),
                            "contains" => query.Where(le => le.Lote.Quinta != null && le.Lote.Quinta.Productor != null && le.Lote.Quinta.Productor.Nombre.Contains(filter.Value!)),
                            _ => query
                            };
                    break;

                case "Variedad":
                    query = filter.Operator switch
                        {
                            "eq" => query.Where(le => le.Lote.Variedad != null && le.Lote.Variedad.Nombre == filter.Value),
                            "contains" => query.Where(le => le.Lote.Variedad != null && le.Lote.Variedad.Nombre.Contains(filter.Value!)),
                            _ => query
                            };
                    break;

                case "Especie":
                    // Corregido: comparaba/filtraba contra la entidad Especie completa en vez
                    // de contra Especie.Descripcion (el texto real que el usuario tipea).
                    query = filter.Operator switch
                        {
                            "eq" => query.Where(le => le.Lote.Variedad != null && le.Lote.Variedad.Especie != null && le.Lote.Variedad.Especie.Descripcion == filter.Value),
                            "contains" => query.Where(le => le.Lote.Variedad != null && le.Lote.Variedad.Especie != null && le.Lote.Variedad.Especie.Descripcion.Contains(filter.Value!)),
                            _ => query
                            };
                    break;

                case "Zafra":
                    query = filter.Operator switch
                        {
                            "eq" => query.Where(le => (le.Zafra ?? le.Lote.Zafra) == filter.Value),
                            "contains" => query.Where(le => (le.Zafra ?? le.Lote.Zafra) != null && (le.Zafra ?? le.Lote.Zafra)!.Contains(filter.Value!)),
                            _ => query
                            };
                    break;
                }
            }

        return query;
        }

    // Helper genérico para eq/gt/lt/between sobre campos int, construido con Expression trees
    // (parametrizado por EF Core al traducir a SQL — sin riesgo de injection).
    private static IQueryable<LoteEmboquille> ApplyIntFilter(
        IQueryable<LoteEmboquille> query,
        Filter filter,
        Expression<Func<LoteEmboquille, int>> selector)
        {
        var param = selector.Parameters[0];
        Expression? body = null;

        switch (filter.Operator)
            {
            case "eq" when int.TryParse(filter.Value, out var eqVal):
                body = Expression.Equal(selector.Body, Expression.Constant(eqVal));
                break;
            case "gt" when int.TryParse(filter.Value, out var gtVal):
                body = Expression.GreaterThan(selector.Body, Expression.Constant(gtVal));
                break;
            case "lt" when int.TryParse(filter.Value, out var ltVal):
                body = Expression.LessThan(selector.Body, Expression.Constant(ltVal));
                break;
            case "between" when int.TryParse(filter.Value, out var fromVal) && int.TryParse(filter.ValueTo, out var toVal):
                var ge = Expression.GreaterThanOrEqual(selector.Body, Expression.Constant(fromVal));
                var le = Expression.LessThanOrEqual(selector.Body, Expression.Constant(toVal));
                body = Expression.AndAlso(ge, le);
                break;
            }

        if (body == null)
            return query;

        var lambda = Expression.Lambda<Func<LoteEmboquille, bool>>(body, param);
        return query.Where(lambda);
        }

    private List<StockPlayaDto> GetDummyData()
        {
        var random = new Random();
        var productores = new[] { "Frutas del Sol", "Agrícola San Juan", "Cítricos del Valle", "Huerta San Rafael", "Cosechas del Este" };
        var variedades = new[] { "Valencia", "Navel", "Satsuma", "Murcott", "Eureka", "Lisboa", "Marsh", "Star Ruby" };

        var especies = new[] { "Naranja", "Mandarina", "Limón", "Pomelo" };

        var data = new List<StockPlayaDto>();

        for (int i = 1; i <= 25; i++)
            {
            data.Add(new StockPlayaDto
                {
                Empaque = i % 2 == 0 ? "Empaque Norte" : "Empaque Sur",
                Especie = especies[random.Next(especies.Length)],
                Variedad = variedades[random.Next(variedades.Length)],
                Lote = 1000 + i,
                Balanza = i % 3 == 0 ? "Báscula 3 - Exportación" : "Báscula 1 - Patio",
                Destino = i % 2 == 0 ? "Cámara Fría" : "Distribución",
                Feccha = DateTime.Now.AddDays(-random.Next(1, 30)).ToString("yyyy.MM.dd"),
                Chacra = $"Chacra {random.Next(1, 6)}",
                Productor = productores[random.Next(productores.Length)],
                TipoLote = i % 3 == 0 ? "Exportación" : "Mercado Interno",
                Ingresados = random.Next(30, 100),
                Volcados = random.Next(10, 40),
                Saldo = random.Next(5, 30),
                KgIngreso = random.Next(1000, 5000),
                KgVolcados = random.Next(300, 1500),
                KgStock = random.Next(200, 800)
                });
            }
        return data;
        }
    }

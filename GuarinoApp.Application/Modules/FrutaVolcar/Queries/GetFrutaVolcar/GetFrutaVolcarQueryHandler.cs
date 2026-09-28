using GuarinoApp.Application.Common.Builders;
using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.FrutaVolcar.Dtos;
using GuarinoApp.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GuarinoApp.Application.Modules.FrutaVolcar.Queries.GetFrutaVolcar;

public class GetFrutaVolcarQueryHandler : IRequestHandler<GetFrutaVolcarQuery, List<FrutaVolcarDto>>
    {
    private readonly IApplicationDbContext _context;

    public GetFrutaVolcarQueryHandler(IApplicationDbContext context)
        {
        _context = context;
        }

    public async Task<List<FrutaVolcarDto>> Handle(GetFrutaVolcarQuery request, CancellationToken cancellationToken)
        {
        // 1. Cargar explícitamente todas las entidades relacionadas
        var lotes = await _context.Lotes
            .Include(l => l.Variedad)
                .ThenInclude(v => v.Especie)
            .Include(l => l.Quinta)
            .Include(l => l.TipoLote)
            .Include(l => l.Productor)
            .Include(l => l.Bascula)
            .Include(l => l.DestinoInterno)
            .Include(l => l.Empaque) // Esto carga la entidad Empaque, no un string
            .AsNoTracking()
            .ToListAsync();

        // 2. Aplicar filtros dinámicos
        if (request.Filters != null && request.Filters.Any())
            {
            var query = _context.Lotes
                .Include(l => l.Variedad)
                    .ThenInclude(v => v.Especie)
                .Include(l => l.Quinta)
                .Include(l => l.TipoLote)
                .Include(l => l.Productor)
                .Include(l => l.Bascula)
                .Include(l => l.DestinoInterno)
                .Include(l => l.Empaque);

            query = (Microsoft.EntityFrameworkCore.Query.IIncludableQueryable<Lote, Domain.Entities.Empaque>)LinqDynamicQueryBuilder
                .ApplyFilters(query, request.Filters);

            var filteredLotes = await query.ToListAsync(cancellationToken);

            // Proyecto los datos filtrados

            return filteredLotes.Select(l => new FrutaVolcarDto
                {
                Lote = l.Id,

                Playa = l.Empaque.Descripcion,

                Especie = l.Variedad?.Especie?.Descripcion ?? string.Empty,
                Variedad = l.Variedad?.Nombre ?? string.Empty,
                TipoLote = l.TipoLote?.Descripcion ?? string.Empty,
                FIngreso = l.FechaIngreso?.ToString("yyyy.MM.dd") ?? string.Empty,
                Productor = l.Productor?.RazonSocial ?? string.Empty,
                Quinta = l.Quinta?.Nombre ?? string.Empty,
                Remitos = l.IdRemito ?? string.Empty,
                Balanza = l.Bascula?.Descripcion ?? string.Empty,
                Destino = l.DestinoInterno?.Descripcion ?? string.Empty,

                CantBins = l.CantidadBins - l.CantidadBinsDesp,
                Volcados = l.CantidadBinsEmb,
                Saldo = l.CantidadBins - l.CantidadBinsDesp - l.CantidadBinsEmb,

                KgIngreso = (int)l.KilosNetos,

                KgVolcados = l.CantidadBins > 0
                    ? (int)((l.KilosNetos / l.CantidadBins) * l.CantidadBinsEmb)
                    : 0,

                KgSaldo = l.CantidadBins > 0
                    ? (int)((l.KilosNetos / l.CantidadBins) * (l.CantidadBins - l.CantidadBinsDesp - l.CantidadBinsEmb))
                    : 0
                }).ToList();
            }

        // Si no hay filtros, simplemente retornar todos los lotes con proyección
        var result = lotes.Select(l => new FrutaVolcarDto
            {
            Lote = l.Id,
            Playa = l.Empaque?.Descripcion ?? string.Empty,
            Especie = l.Variedad?.Especie?.Descripcion ?? string.Empty,
            Variedad = l.Variedad?.Nombre ?? string.Empty,
            TipoLote = l.TipoLote?.Descripcion ?? string.Empty,
            FIngreso = l.FechaIngreso?.ToString("yyyy.MM.dd") ?? string.Empty,
            Productor = l.Productor?.RazonSocial ?? string.Empty,
            Quinta = l.Quinta?.Nombre ?? string.Empty,
            Remitos = l.IdRemito ?? string.Empty,
            Balanza = l.Bascula?.Descripcion ?? string.Empty,
            Destino = l.DestinoInterno?.Descripcion ?? string.Empty,

            CantBins = l.CantidadBins - l.CantidadBinsDesp,
            Volcados = l.CantidadBinsEmb,
            Saldo = l.CantidadBins - l.CantidadBinsDesp - l.CantidadBinsEmb,

            KgIngreso = (int)l.KilosNetos,

            KgVolcados = l.CantidadBins > 0
                ? (int)((l.KilosNetos / l.CantidadBins) * l.CantidadBinsEmb)
                : 0,

            KgSaldo = l.CantidadBins > 0
                ? (int)((l.KilosNetos / l.CantidadBins) * (l.CantidadBins - l.CantidadBinsDesp - l.CantidadBinsEmb))
                : 0
            }).ToList();

        return result;
        }

    }

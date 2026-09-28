csharp C:\Guarino\GuarinoApp.Application\Common\Interfaces\IApplicationDbContext.cs
using GuarinoApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GuarinoApp.Application.Common.Interfaces
{
    public interface IApplicationDbContext
    {
        DbSet<Lote> Lotes { get; }
        DbSet<Volcado> Volcados { get; }
        DbSet<Pallet> Pallets { get; }
        DbSet<Productor> Productores { get; }
        DbSet<Quinta> Quintas { get; }
        DbSet<Variedad> Variedades { get; }
        DbSet<Especie> Especies { get; }
        DbSet<Bascula> Basculas { get; }
        DbSet<DestinoInterno> DestinosInternos { get; }
        DbSet<TipoLote> TiposLote { get; }
        DbSet<ProductorChacra> ProductoresChacra { get; }
        DbSet<Responsable> Responsables { get; }
        DbSet<Emboquille> Emboquilles { get; }
        DbSet<LoteEmboquille> LotesEmboquille { get; }
        DbSet<Empaque> Empaques { get; }
        DbSet<VolcadoHistorico> VolcadosHistoricos { get; }
        DbSet<StockVariedad> StocksVariedad { get; }

        // Lectura de tablas externas/vistas keyless si es necesario
        DbSet<RitmoCalibradora> RitmosCalibradora { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
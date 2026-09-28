# Ejecutar en PowerShell 7+ desde C:\Guarino
Set-StrictMode -Version Latest

# Función auxiliar para escribir con UTF-8 sin BOM
function Write-FileNoBom([string]$path, [string]$content) {
    $dir = [System.IO.Path]::GetDirectoryName($path)
    if (-not (Test-Path $dir)) { New-Item -Path $dir -ItemType Directory -Force | Out-Null }
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($content)
    [System.IO.File]::WriteAllBytes($path, $bytes)
    Write-Host "Wrote $path"
}

# Contenidos (reemplazar si ya existen)
$files = @{
    "GuarinoApp.Application\Modules\PalletsSinCliente\Dtos\PalletSinClienteDto.cs" = @"
namespace GuarinoApp.Application.Modules.PalletsSinCliente.Dtos
{
    public class PalletSinClienteDto
    {
        public int Id { get; set; }
        public string? Codigo { get; set; }
        public string? Variedad { get; set; }
        public string? Estado { get; set; }
        public DateTime? Fecha { get; set; }
        public string? Cliente { get; set; }
    }
}
"@

    "GuarinoApp.Domain\Entities\RitmoCalibradora.cs" = @"
namespace GuarinoApp.Domain.Entities
{
    /// <summary>
    /// Representa filas de RITMOCALIBRADORA (solo lectura). Keyless para consultas con EF Core.
    /// </summary>
    public class RitmoCalibradora
    {
        public string? Loteroda { get; set; }
        public DateTime? Fecha { get; set; }
        public int? IdEspecie { get; set; }
        public int? IdVariedad { get; set; }
        public decimal Calibradora { get; set; }
        public decimal DcteSincera { get; set; }
        public decimal Volcado { get; set; }
        public decimal Hscal { get; set; }
        public decimal Hspar { get; set; }
        public decimal Ritmo1 { get; set; }
        public decimal Ritmo2 { get; set; }
        public string? Observacion { get; set; }
    }
}
"@

    "GuarinoApp.Application\Common\Interfaces\IApplicationDbContext.cs" = @"
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

        // Lectura keyless / vistas externas
        DbSet<RitmoCalibradora> RitmosCalibradora { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
"@

    "GuarinoApp.Infrastructure\Data\ApplicationDbContext.cs" = @"
using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Linq;

namespace GuarinoApp.Infrastructure.Data
{
    public class ApplicationDbContext : DbContext, IApplicationDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<Lote> Lotes { get; set; }
        public DbSet<Volcado> Volcados { get; set; }
        public DbSet<Pallet> Pallets { get; set; }
        public DbSet<Productor> Productores { get; set; }
        public DbSet<Quinta> Quintas { get; set; }
        public DbSet<Variedad> Variedades { get; set; }
        public DbSet<Especie> Especies { get; set; }
        public DbSet<Bascula> Basculas { get; set; }
        public DbSet<DestinoInterno> DestinosInternos { get; set; }
        public DbSet<TipoLote> TiposLote { get; set; }
        public DbSet<ProductorChacra> ProductoresChacra { get; set; }
        public DbSet<Responsable> Responsables { get; set; }
        public DbSet<Emboquille> Emboquilles { get; set; }
        public DbSet<LoteEmboquille> LotesEmboquille { get; set; }
        public DbSet<Empaque> Empaques { get; set; }
        public DbSet<VolcadoHistorico> VolcadosHistoricos { get; set; }
        public DbSet<StockVariedad> StocksVariedad { get; set; }

        // Entidad keyless para la tabla/vista RITMOCALIBRADORA (solo lectura)
        public DbSet<RitmoCalibradora> RitmosCalibradora { get; set; }

        public async Task<List<T>> ExecuteSqlRawAsync<T>(string sql, params object[] parameters) where T : class, new()
        {
            var result = new List<T>();

            var connection = Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandType = CommandType.Text;

            foreach (var param in parameters)
            {
                if (param is DbParameter dbParam)
                {
                    command.Parameters.Add(dbParam);
                }
            }

            var properties = typeof(T)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite)
                .ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var item = new T();

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var columnName = reader.GetName(i);
                    if (!properties.TryGetValue(columnName, out var prop))
                        continue;

                    var value = reader.GetValue(i);
                    if (value is DBNull)
                    {
                        prop.SetValue(item, null);
                        continue;
                    }

                    var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

                    try
                    {
                        var convertedValue = targetType.IsEnum
                            ? Enum.ToObject(targetType, value)
                            : Convert.ChangeType(value, targetType);
                        prop.SetValue(item, convertedValue);
                    }
                    catch
                    {
                        // ignorar conversión fallida para esa columna
                    }
                }

                result.Add(item);
            }

            return result;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Pallet>()
                .Property(p => p.PesoNeto)
                .HasPrecision(18, 2);

            modelBuilder.Entity<VolcadoHistorico>()
                .Property(x => x.KgsVolcados)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Volcado>()
                .Property(x => x.PesoKilos)
                .HasPrecision(18, 2);

            modelBuilder.Entity<LoteEmboquille>()
                .Property(x => x.KilosNetos)
                .HasPrecision(18, 2);

            // Mapear RitmoCalibradora como keyless (tabla o sinónimo en BD)
            modelBuilder.Entity<RitmoCalibradora>(eb =>
            {
                eb.HasNoKey();
                eb.ToTable(""RITMOCALIBRADORA"");
                eb.Property(r => r.Loteroda).HasColumnName(""loteroda"");
                eb.Property(r => r.Fecha).HasColumnName(""fecha"");
                eb.Property(r => r.IdEspecie).HasColumnName(""idespecie"");
                eb.Property(r => r.IdVariedad).HasColumnName(""idvariedad"");
                eb.Property(r => r.Calibradora).HasColumnName(""calibradora"");
                eb.Property(r => r.DcteSincera).HasColumnName(""dcte_sincera"");
                eb.Property(r => r.Volcado).HasColumnName(""volcado"");
                eb.Property(r => r.Hscal).HasColumnName(""hscal"");
                eb.Property(r => r.Hspar).HasColumnName(""hspar"");
                eb.Property(r => r.Ritmo1).HasColumnName(""ritmo1"");
                eb.Property(r => r.Ritmo2).HasColumnName(""ritmo2"");
                eb.Property(r => r.Observacion).HasColumnName(""observacion"");
            });
        }
    }
}
"@
}

# Escribir archivos
foreach ($rel in $files.Keys) {
    $full = Join-Path (Get-Location) $rel
    Write-FileNoBom $full $files[$rel]
}

# Limpieza bin/obj
Write-Host "Eliminando carpetas bin/obj..."
Get-ChildItem -Path . -Recurse -Directory -Include bin,obj -Force -ErrorAction SilentlyContinue | ForEach-Object { Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue }

# Restaurar y compilar
Write-Host "Restaurando y compilando..."
dotnet restore
$code = dotnet build -v minimal
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build falló. Revisa la salida anterior." -ForegroundColor Red
} else {
    Write-Host "Build OK" -ForegroundColor Green
}

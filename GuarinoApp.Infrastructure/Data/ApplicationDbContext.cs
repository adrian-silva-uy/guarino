using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GuarinoApp.Infrastructure.Data
    {
    /// <summary>
    /// DbContext de la aplicación. Contiene DbSet<> para las entidades del dominio
    /// y el mapeo keyless necesario para reportes (RITMOCALIBRADORA).
    /// </summary>
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

        // Nuevo: reemplazan el SQL crudo de GetLugares*QueryHandler (ver OnModelCreating para
        // el mapeo explícito de tabla/columnas -- las 3 tablas YA EXISTEN en la base real).
        public DbSet<Lugar> Lugares { get; set; }
        public DbSet<LugarOrigen> LugaresOrigen { get; set; }
        public DbSet<LugarDestino> LugaresDestino { get; set; }

        // Entidad keyless (solo lectura)
        public DbSet<RitmoCalibradora> RitmosCalibradora { get; set; }

        public DbSet<EventoClimatico> EventosClimaticos => Set<EventoClimatico>();
        public DbSet<FinancialData> FinancialData => Set<FinancialData>();
        public DbSet<LogEntry> LogEntries => Set<LogEntry>();
        public DbSet<AppUser> Users => Set<AppUser>();
        public DbSet<Role> Roles => Set<Role>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
            base.OnModelCreating(modelBuilder);

            // --- Precisión para decimales existentes ---
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

            // --- Precisión para decimales de Contaduría (FinancialData) ---
            modelBuilder.Entity<FinancialData>()
                .Property(f => f.CostoPorKg)
                .HasPrecision(18, 2);

            modelBuilder.Entity<FinancialData>()
                .Property(f => f.FlujoCaja)
                .HasPrecision(18, 2);

            modelBuilder.Entity<FinancialData>()
                .Property(f => f.MargenBruto)
                .HasPrecision(18, 2);

            modelBuilder.Entity<FinancialData>()
                .Property(f => f.CostoFlete)
                .HasPrecision(18, 2);

            modelBuilder.Entity<FinancialData>()
                .Property(f => f.CapacidadInstalada)
                .HasPrecision(18, 2);

            // corregir advertencia en Lote
            modelBuilder.Entity<Lote>()
                .Property(l => l.KilosNetos)
                .HasPrecision(18, 2);

            // --- Precisión para Coordenadas GPS en Lote ---
            // 10 dígitos totales, 6 decimales (precisión de ~11 cm)
            modelBuilder.Entity<Lote>()
                .Property(l => l.Latitude)
                .HasPrecision(10, 6);

            modelBuilder.Entity<Lote>()
                .Property(l => l.Longitude)
                .HasPrecision(10, 6);

            // --- Relaciones ---
            modelBuilder.Entity<Lote>()
                .HasOne(l => l.TipoLote)
                .WithMany()
                .HasForeignKey(l => l.TipoLoteId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Lote>()
                .HasOne(l => l.Productor)
                .WithMany()
                .HasForeignKey(l => l.ProductorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Lote>()
                .HasOne(l => l.Empaque)
                .WithMany()
                .HasForeignKey(l => l.EmpaqueId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<LoteEmboquille>()
                .HasOne(le => le.Bascula)
                .WithMany()
                .HasForeignKey(le => le.IdBascula)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<LoteEmboquille>()
                .HasOne(le => le.DestinoInterno)
                .WithMany()
                .HasForeignKey(le => le.IdDestinoInterno)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<LoteEmboquille>()
                .HasOne(le => le.Empaque)
                .WithMany()
                .HasForeignKey(le => le.IdEmpaque)
                .OnDelete(DeleteBehavior.Restrict);

            // Lote/Emboquille: antes eran relaciones por convención con FK shadow
            // (LoteId/EmboquilleId) mientras la entidad declaraba IdLote/IdEmboquille
            // como columnas muertas — 4 columnas para 2 relaciones. Ahora la entidad
            // declara las FKs reales y el mapeo es explícito.
            modelBuilder.Entity<LoteEmboquille>()
                .HasOne(le => le.Lote)
                .WithMany()
                .HasForeignKey(le => le.LoteId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<LoteEmboquille>()
                .HasOne(le => le.Emboquille)
                .WithMany()
                .HasForeignKey(le => le.EmboquilleId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Variedad>()
                .HasOne(v => v.Especie)
                .WithMany()
                .HasForeignKey(v => v.EspecieId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Lote>()
                .HasOne(l => l.Variedad)
                .WithMany()
                .HasForeignKey(l => l.VariedadId)
                .OnDelete(DeleteBehavior.Restrict);

            // --- Nuevo: mapeo explícito de Lugares / LugarOrigen / LugarDestino ---
            modelBuilder.Entity<Lugar>(eb =>
            {
                eb.ToTable("Lugares");
                eb.HasKey(l => l.Id);
                eb.Property(l => l.Id).HasColumnName("Id");
                eb.Property(l => l.Nombre).HasColumnName("Nombre");
                eb.Property(l => l.Tipo).HasColumnName("Tipo");
                eb.Property(l => l.Direccion).HasColumnName("Direccion");
            });

            modelBuilder.Entity<LugarOrigen>(eb =>
            {
                eb.ToTable("LugarOrigen");
                eb.HasKey(l => l.IdLugarOrigen);
                eb.Property(l => l.IdLugarOrigen).HasColumnName("IdLugarOrigen");
                eb.Property(l => l.Nombre).HasColumnName("Nombre");
                eb.Property(l => l.Direccion).HasColumnName("Direccion");
                eb.Property(l => l.Localidad).HasColumnName("Localidad");
            });

            modelBuilder.Entity<LugarDestino>(eb =>
            {
                eb.ToTable("LugarDestino");
                eb.HasKey(l => l.IdLugarDestino);
                eb.Property(l => l.IdLugarDestino).HasColumnName("IdLugarDestino");
                eb.Property(l => l.Nombre).HasColumnName("Nombre");
                eb.Property(l => l.Direccion).HasColumnName("Direccion");
                eb.Property(l => l.Localidad).HasColumnName("Localidad");
            });

            // --- Usuarios de la aplicación (autenticación local) ---
            modelBuilder.Entity<AppUser>(entity =>
            {
                entity.ToTable("AppUsers");
                entity.HasKey(u => u.Id);
                entity.Property(u => u.UserName).IsRequired().HasMaxLength(100);
                entity.Property(u => u.FullName).IsRequired().HasMaxLength(200);
                entity.Property(u => u.PasswordHash).IsRequired().HasMaxLength(200);
                // Rol normalizado: FK a la tabla Roles (nunca cadenas sueltas).
                entity.Property(u => u.RoleId).IsRequired();
                entity.HasOne(u => u.Role)
                    .WithMany()
                    .HasForeignKey(u => u.RoleId)
                    .OnDelete(DeleteBehavior.Restrict);
                // Concurrencia optimista: dos sesiones que editen al mismo
                // usuario no se pisan; la segunda recibe un error controlado.
                entity.Property(u => u.RowVersion).IsRowVersion();
                // La collation por defecto de SQL Server es case-insensitive,
                // por lo que el índice único impedirá "ADRIA" y "adria" a la vez.
                entity.HasIndex(u => u.UserName).IsUnique();
            });

            // --- Roles de acceso (tabla maestra) ---
            modelBuilder.Entity<Role>(entity =>
            {
                entity.ToTable("Roles");
                entity.HasKey(r => r.Id);
                entity.Property(r => r.Name).IsRequired().HasMaxLength(50);
                entity.Property(r => r.IsActive).IsRequired();
                entity.HasIndex(r => r.Name).IsUnique();
            });

            // --- Entidad keyless ---
            modelBuilder.Entity<RitmoCalibradora>(eb =>
            {
                eb.HasNoKey();
                eb.ToView("vRitmoCalibradora");
                eb.Property(r => r.Loteroda).HasColumnName("loteroda");
                eb.Property(r => r.Fecha).HasColumnName("fecha");
                eb.Property(r => r.IdEspecie).HasColumnName("idespecie");
                eb.Property(r => r.Especie).HasColumnName("especie");
                eb.Property(r => r.Variedad).HasColumnName("variedad");
                eb.Property(r => r.IdVariedad).HasColumnName("idvariedad");
                eb.Property(r => r.Calibradora).HasColumnName("calibradora").HasPrecision(18, 6);
                eb.Property(r => r.DcteSincera).HasColumnName("dcte_sincera").HasPrecision(18, 6);
                eb.Property(r => r.Volcado).HasColumnName("volcado").HasPrecision(18, 6);
                eb.Property(r => r.Hscal).HasColumnName("hscal").HasPrecision(18, 6);
                eb.Property(r => r.Hspar).HasColumnName("hspar").HasPrecision(18, 6);
                eb.Property(r => r.Ritmo1).HasColumnName("ritmo1").HasPrecision(18, 6);
                eb.Property(r => r.Ritmo2).HasColumnName("ritmo2").HasPrecision(18, 6);
                eb.Property(r => r.Observacion).HasColumnName("observacion");
            });

            // --- Configuración de EventoClimatico ---
            modelBuilder.Entity<EventoClimatico>(entity =>
            {
                entity.ToTable("EventosClimaticos");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.PorcentajePerdidaEstimada)
                      .HasPrecision(18, 4); // Hasta 4 decimales (ej. 12.3456%)

                entity.Property(e => e.KilosPerdidosEstimados)
                      .HasPrecision(18, 2); // 2 decimales para kilos (ej. 1000.50 kg)

                entity.Property(e => e.TipoEvento)
                      .IsRequired()
                      .HasMaxLength(50);

                entity.Property(e => e.SeveridadEstimada)
                      .IsRequired()
                      .HasMaxLength(20);

                entity.Property(e => e.Fuente)
                      .IsRequired()
                      .HasMaxLength(100);

                // Índices para consultas rápidas
                entity.HasIndex(e => e.LoteId);
                entity.HasIndex(e => e.Fecha);
                entity.HasIndex(e => e.TipoEvento);

                // Relaciones
                entity.HasOne(e => e.Lote)
                      .WithMany()
                      .HasForeignKey(e => e.LoteId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Productor)
                      .WithMany()
                      .HasForeignKey(e => e.ProductorId)
                      .OnDelete(DeleteBehavior.Restrict);
            });
            }
        }
    }

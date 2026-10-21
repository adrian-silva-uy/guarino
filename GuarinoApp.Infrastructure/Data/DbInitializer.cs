using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using GuarinoApp.Domain.Entities;
using GuarinoApp.Domain.Enums;
using GuarinoApp.Infrastructure.Services.Auth;
using Microsoft.EntityFrameworkCore;

namespace GuarinoApp.Infrastructure.Data
    {
    /// <summary>
    /// Inicializador de datos de prueba (sólo Development). Cada bloque está
    /// protegido por un guard por tabla (idempotente): si la tabla ya tiene
    /// filas — p. ej. datos reales o cargas previas — jamás se pisa.
    /// </summary>
    public static class DbInitializer
        {
        public static void Initialize(ApplicationDbContext context, string? emailAdmin = null)
            {
            var random = new Random(12345);

            // 1. Seedear Productores
            if (!context.Productores.Any())
                {
                context.Productores.AddRange(
                    new Productor { Nombre = "Frutas del Sol" },
                    new Productor { Nombre = "Agrícola San Juan" },
                    new Productor { Nombre = "Viña del Valle" },
                    new Productor { Nombre = "Huerta Orgánica" },
                    new Productor { Nombre = "Cosechas del Este" }
                );
                context.SaveChanges(); // Guardamos para obtener IDs
                }

            // 1.5 Seedear Especies
            if (!context.Especies.Any())
                {
                context.Especies.AddRange(
                    new Especie { Descripcion = "Naranja" },
                    new Especie { Descripcion = "Mandarina" },
                    new Especie { Descripcion = "Limón" },
                    new Especie { Descripcion = "Pomelo" }
                );
                context.SaveChanges();
                }

            var especies = context.Especies.ToList();
            var especieNaranja = especies.FirstOrDefault(e => e.Descripcion == "Naranja");
            var especieMandarina = especies.FirstOrDefault(e => e.Descripcion == "Mandarina");
            var especieLimon = especies.FirstOrDefault(e => e.Descripcion == "Limón");
            var especiePomelo = especies.FirstOrDefault(e => e.Descripcion == "Pomelo");

            // 2. Seedear Variedades
            if (!context.Variedades.Any())
                {
                context.Variedades.AddRange(
                    new Variedad { Nombre = "Valencia", EspecieId = especieNaranja?.Id },
                    new Variedad { Nombre = "Navel", EspecieId = especieNaranja?.Id },
                    new Variedad { Nombre = "Satsuma", EspecieId = especieMandarina?.Id },
                    new Variedad { Nombre = "Murcott", EspecieId = especieMandarina?.Id },
                    new Variedad { Nombre = "Eureka", EspecieId = especieLimon?.Id },
                    new Variedad { Nombre = "Lisboa", EspecieId = especieLimon?.Id },
                    new Variedad { Nombre = "Marsh", EspecieId = especiePomelo?.Id },
                    new Variedad { Nombre = "Star Ruby", EspecieId = especiePomelo?.Id }
                );
                context.SaveChanges();
                }

            // 3. Seedear Quintas (Antes de Lotes)
            if (!context.Quintas.Any())
                {
                // Obtener el primer productor disponible para asignarlo
                // Nota: Asegúrate de que 'productores' ya esté cargado o recárgalo aquí si es necesario
                // En tu código original, 'productores' se obtiene después de 'Especies' y 'Variedades', 
                // pero antes de 'Quintas' no se había definido esta variable.
                // Vamos a cargarla explícitamente aquí para evitar ambigüedades.
                var quintasProductores = context.Productores.ToList();
                if (!quintasProductores.Any())
                    {
                    // Fallback si por alguna razón no hay productores (no debería pasar)
                    throw new InvalidOperationException("No se encontraron Productores. Asegúrate de que el seed de Productores se ejecute correctamente.");
                    }
                var primerProductor = quintasProductores.First();

                context.Quintas.AddRange(
                    new Quinta { Nombre = "La Esperanza", ProductorId = primerProductor.Id },
                    new Quinta { Nombre = "Los Alamos", ProductorId = primerProductor.Id },
                    new Quinta { Nombre = "El Sol", ProductorId = primerProductor.Id }
                );
                context.SaveChanges();
                }

            // 4. Seedear Empaques (Antes de Lotes)
            if (!context.Empaques.Any())
                {
                context.Empaques.AddRange(
                    new Empaque { Descripcion = "Empaque Central" },
                    new Empaque { Descripcion = "Empaque Norte" },
                    new Empaque { Descripcion = "Empaque Sur" }
                );
                context.SaveChanges(); // Guardamos para tener IDs reales
                }

            // 4.5 Catálogos operativos: Básculas (IDs exactos 1-4), Emboquilles,
            // TiposLote, DestinosInternos, Responsables, Lugares y ProductoresChacra.
            SembrarCatalogosOperativos(context);

            // Obtener IDs para asignación
            var variedades = context.Variedades.ToList();
            var productores = context.Productores.ToList();
            var quintas = context.Quintas.ToList();
            var empaques = context.Empaques.ToList();

            if (variedades.Count == 0 || productores.Count == 0 || quintas.Count == 0 || empaques.Count == 0)
                {
                context.SaveChanges();
                return;
                }

            // 5. Seed Lote (Con FKs válidas)
            if (!context.Lotes.Any())
                {
                var lotes = new List<Lote>();
                var fechaBase = DateTime.Now.AddMonths(-12);

                for (int i = 0; i < 150; i++)
                    {
                    var variedad = variedades[random.Next(variedades.Count)];
                    var productor = productores[random.Next(productores.Count)];
                    var quinta = quintas[random.Next(quintas.Count)];
                    var empaque = empaques[random.Next(empaques.Count)];

                    lotes.Add(new Lote
                        {
                        VariedadId = variedad.Id,
                        ProductorId = productor.Id,
                        QuintaId = quinta.Id,
                        EmpaqueId = empaque.Id,
                        CantidadBins = random.Next(20, 150),
                        FechaIngreso = fechaBase.AddMonths(random.Next(0, 12)),
                        Zafra = DateTime.Now.Year.ToString(),
                        Codigo = $"LOTE-{DateTime.Now.Year}-{i + 1:0000}"
                        });
                    }
                context.Lotes.AddRange(lotes);
                // Guardamos ya: los bloques siguientes (CompletarLotes,
                // LotesEmboquille, EventosClimaticos, RitmosCalibradora)
                // necesitan los IDs reales de Lotes.
                context.SaveChanges();
                }

            // 5.5 Completar Lotes: coordenadas GPS (zona Salto), KilosNetos,
            // FKs a catálogos y remito — sólo campos vacíos/cero.
            CompletarLotes(context, random);
            context.SaveChanges();

            // 5.6 Completar Productores: RazonSocial y Ruc cuando están vacíos.
            CompletarProductores(context);
            context.SaveChanges();

            // 5.7 LotesEmboquille: pesajes/emboquilles históricos (>= 2 temporadas,
            // picos may-sep) que alimentan Stock Playa y la Gráfica de Procesos.
            SembrarLotesEmboquille(context, random);

            // 6. Seed VolcadosHistoricos
            if (!context.VolcadosHistoricos.Any())
                {
                var volcados = new List<VolcadoHistorico>();
                var fechaBase = DateTime.Now.AddMonths(-24);

                for (int i = 0; i < 24; i++)
                    {
                    var fecha = fechaBase.AddMonths(i);
                    var mes = fecha.Month;
                    var año = fecha.Year;
                    var factorEstacional = (mes == 3 || mes == 6 || mes == 9 || mes == 12) ? 1.4 : 1.0;
                    var baseValue = 12000 + (mes * 500) + (año - 2024) * 2000;

                    foreach (var variedad in variedades)
                        {
                        var kgs = (baseValue * factorEstacional * (0.8 + (random.NextDouble() * 0.4))) * (0.8 + (random.NextDouble() * 0.4));
                        volcados.Add(new VolcadoHistorico
                            {
                            Fecha = fecha,
                            Mes = mes,
                            Año = año,
                            Variedad = variedad.Nombre,
                            KgsVolcados = (decimal)Math.Round(kgs, 0),
                            Bins = random.Next(10, 45),
                            Productor = productores[random.Next(productores.Count)].Nombre,
                            Zafra = año.ToString()
                            });
                        }
                    }
                context.VolcadosHistoricos.AddRange(volcados);
                }

            // 7. Seed StocksVariedad
            if (!context.StocksVariedad.Any())
                {
                var stocks = new List<StockVariedad>();
                var fechaBaseStock = DateTime.Now.AddMonths(-12);
                var coloresVariedad = new[] { "#4caf50", "#2196f3", "#ff9800", "#f44336", "#9c27b0", "#00bcd4", "#ff5722", "#607d8b" };

                for (int i = 0; i < 12; i++)
                    {
                    var fecha = fechaBaseStock.AddMonths(i);
                    var mes = fecha.Month;
                    var año = fecha.Year;

                    for (int idx = 0; idx < variedades.Count; idx++)
                        {
                        var variedad = variedades[idx];
                        var color = coloresVariedad[idx % coloresVariedad.Length];
                        var baseStock = 120 - (idx * 15);
                        var variacion = random.Next(-15, 20);
                        var stock = Math.Max(baseStock + variacion, 10);

                        stocks.Add(new StockVariedad
                            {
                            Fecha = fecha,
                            Mes = mes,
                            Año = año,
                            Variedad = variedad.Nombre,
                            Cantidad = stock,
                            Color = color
                            });
                        }
                    }
                context.StocksVariedad.AddRange(stocks);
                }

            // 8. Seed Pallets
            if (!context.Pallets.Any())
                {
                var calibres = new[] { "100", "110", "120", "135", "150" };
                var envases = new[] { "Caja 18kg", "Caja 10kg", "Bandeja 40x60" };
                var marcas = new[] { "Guarino Premium", "Sol de Invierno", "Export Quality" };
                var clientes = new[] { "Frutas del Sur S.A.", "Importadora San Martín", "Global Fruit LLC" };

                var palletsMock = new List<Pallet>();
                var fechaInicioPallet = DateTime.Now.AddDays(-60);
                EstadoPallet estadoPorDefecto = default;
                if (Enum.GetNames<EstadoPallet>().Length > 0)
                    {
                    // Esto evita la ambigüedad de tipos y el error de "grupo de métodos"
                    estadoPorDefecto = Enum.GetValues<EstadoPallet>().Cast<EstadoPallet>().First();
                    }

                for (int i = 1; i <= 50; i++)
                    {
                    var fechaPallet = fechaInicioPallet.AddHours(i * 12);
                    var variedadRandom = variedades[random.Next(variedades.Count)];

                    palletsMock.Add(new Pallet
                        {
                        Codigo = $"PAL-{2024000 + i}",
                        Calibre = calibres[random.Next(calibres.Length)],
                        Envase = envases[random.Next(envases.Length)],
                        Marca = marcas[random.Next(marcas.Length)],
                        Contramarca = $"CM-{i}",
                        Cliente = clientes[random.Next(0, clientes.Length)],
                        Categoria = "Primera",
                        Estado = estadoPorDefecto,
                        CantidadEnvases = random.Next(40, 80),
                        CreatedAt = fechaPallet,
                        IsDeleted = false,
                        Variedad = variedadRandom.Nombre,
                        Productor = productores[random.Next(productores.Count)].Nombre
                        });
                    }
                context.Pallets.AddRange(palletsMock);
                }

            // 9. Seed Pallets sin Cliente
            if (!context.Pallets.Any(p => !p.IsDeleted && (p.Cliente == null || string.IsNullOrWhiteSpace(p.Cliente))))
                {
                var calibresPendientes = new[] { "100", "110", "120", "135" };
                var contramarcasPendientes = new[] { "NANAK", "SUNKIST", "DEL MONTE", "DOLCE" };
                var fechasPendientes = new[]
                {
                    new DateTime(2025, 9, 15), new DateTime(2025, 10, 8), new DateTime(2025, 11, 19),
                    new DateTime(2025, 12, 4), new DateTime(2026, 1, 16), new DateTime(2026, 2, 10),
                    new DateTime(2026, 3, 22), new DateTime(2026, 4, 6), new DateTime(2026, 5, 18),
                    new DateTime(2026, 6, 27), new DateTime(2026, 7, 9), new DateTime(2026, 8, 5)
                };

                for (int i = 0; i < fechasPendientes.Length; i++)
                    {
                    var variedad = variedades[i % variedades.Count];
                    context.Pallets.Add(new Pallet
                        {
                        Codigo = $"PSC-SEED-{i + 1:000}",
                        PalletNumero = 9000 + i + 1,
                        Cliente = i % 4 == 0 ? null : string.Empty,
                        Variedad = variedad.Nombre,
                        Productor = productores[i % productores.Count].Nombre,
                        Contramarca = contramarcasPendientes[i % contramarcasPendientes.Length],
                        Calibre = calibresPendientes[i % calibresPendientes.Length],
                        Envase = "Caja 18kg",
                        Marca = "Guarino Premium",
                        Categoria = "Primera",
                        Estado = Enum.GetNames(typeof(EstadoPallet)).Length > 0 ? Enum.GetValues(typeof(EstadoPallet)).Cast<EstadoPallet>().First() : default,
                        Cantidad = 1,
                        CantidadEnvases = 56 + (i % 5),
                        PesoNeto = 1008 + (i * 18),
                        Fecha = fechasPendientes[i],
                        CreatedAt = fechasPendientes[i],
                        CreatedBy = "DbInitializer",
                        IsDeleted = false
                        });
                    }
                }

            // 9.5 Completar Pallets sembrados: Produccion, PesoNeto, Estado mixto
            // (EnFrio/PreFrio para la página "Pallets en Frío"), Frio, Especie,
            // SemanaProd, Fecha/Hora, Lote, TipoPallet, Embalaje, etc.
            CompletarPallets(context, random);

            // 9.6 Eventos climáticos: NO se siembra ninguno. Los eventos son datos
            // medidos (escáner Open-Meteo cada 6 h sobre coordenadas de los lotes);
            // eventos sembrados inventados distorsionaban los KPIs financieros.

            // 9.7 vRitmoCalibradora: crea la tabla si no existe (ToView no la crea
            // en migraciones) y la siembra coherente con LotesEmboquille.
            SembrarRitmosCalibradora(context, random);

            // 10. Seed FinancialData (datos de Contaduría para los KPIs de la proyección de zafra)
            // Solo si la tabla está vacía: si Contaduría ya cargó sus cifras, jamás se pisan.
            if (!context.FinancialData.Any())
                {
                context.FinancialData.Add(new FinancialData
                    {
                    CostoPorKg = 0.58m,          // USD por kg procesado (promedio 3 años)
                    FlujoCaja = 45000m,          // USD disponibles en caja
                    MargenBruto = 34m,           // %
                    CostoFlete = 4800m,          // USD por contenedor
                    CapacidadInstalada = 130000m, // kg por zafra sin tercerizar
                    FechaActualizacion = DateTime.Now,
                    ActualizadoPor = "sistema",
                    Observaciones = "Valores iniciales de referencia. Actualizar con las cifras " +
                                    "reales de Contaduría desde el módulo homónimo."
                    });
                }

            // 10.5 Usuarios de aplicación por rol (Gerencia/Contador/Operador).
            // Sólo si ya existe al menos un usuario: se preserva FR-8 (el primer
            // usuario creado manualmente es Admin y su contraseña no se toca).
            SembrarUsuariosDeRol(context);

            // 10.55 Correo de los Admin: los usuarios creados antes de existir
            // la columna Email quedan con NULL y no recibirían reportes.
            AsegurarEmailAdmin(context, emailAdmin);

            // 10.6 LogEntries de auditoría variados (sin datos sensibles).
            SembrarLogEntries(context, random);

            // Save final solo para datos dependientes
            context.SaveChanges();
            }

        // ------------------------------------------------------------------
        // Catálogos operativos
        // ------------------------------------------------------------------

        /// <summary>
        /// Siembra Básculas (IDs exactos 1-4: los datos históricos de playa los
        /// referencian), Emboquilles, TiposLote, DestinosInternos, Responsables,
        /// Lugares/LugarOrigen/LugarDestino y ProductoresChacra.
        /// </summary>
        private static void SembrarCatalogosOperativos(ApplicationDbContext context)
            {
            // Básculas: reseedear el identity para que las 4 filas reciban
            // exactamente los IDs 1-4. Si el DBCC falla (permisos, motor no SQL),
            // se insertan igual con el identity vigente.
            if (!context.Basculas.Any())
                {
                try
                    {
                    context.Database.ExecuteSqlRaw("DBCC CHECKIDENT ('Basculas', RESEED, 0);");
                    }
                catch
                    {
                    // Mejor esfuerzo: los catálogos internos usan los IDs reales.
                    }

                context.Basculas.AddRange(
                    new Bascula { Descripcion = "Báscula 1 - Patio" },
                    new Bascula { Descripcion = "Báscula 2 - Empaque" },
                    new Bascula { Descripcion = "Báscula 3 - Exportación" },
                    new Bascula { Descripcion = "Báscula 4 - Playa" }
                );
                context.SaveChanges();
                }

            if (!context.Emboquilles.Any())
                {
                context.Emboquilles.AddRange(
                    new Emboquille { Descripcion = "Emboquille 1 - Línea 1" },
                    new Emboquille { Descripcion = "Emboquille 2 - Línea 2" },
                    new Emboquille { Descripcion = "Emboquille 3 - Línea 3" }
                );
                context.SaveChanges();
                }

            if (!context.TiposLote.Any())
                {
                context.TiposLote.AddRange(
                    new TipoLote { Descripcion = "Cosecha propia" },
                    new TipoLote { Descripcion = "Compra" },
                    new TipoLote { Descripcion = "Devolución" }
                );
                context.SaveChanges();
                }

            if (!context.DestinosInternos.Any())
                {
                context.DestinosInternos.AddRange(
                    new DestinoInterno { Descripcion = "Calibradora" },
                    new DestinoInterno { Descripcion = "Empaque" },
                    new DestinoInterno { Descripcion = "Playa de Frío" },
                    new DestinoInterno { Descripcion = "Volcado" }
                );
                context.SaveChanges();
                }

            if (!context.Responsables.Any())
                {
                context.Responsables.AddRange(
                    new Responsable { Descripcion = "Juan Pérez" },
                    new Responsable { Descripcion = "María Gómez" },
                    new Responsable { Descripcion = "Carlos Rodríguez" }
                );
                context.SaveChanges();
                }

            // Tablas legacy (mapeadas con ToTable en el DbContext); se siembran
            // sólo si están vacías.
            if (!context.Lugares.Any())
                {
                context.Lugares.AddRange(
                    new Lugar { Nombre = "Patio Principal", Tipo = "Playa", Direccion = "Ruta 3 km 480" },
                    new Lugar { Nombre = "Empaque Central", Tipo = "Empaque", Direccion = "Av. Principal 1234" },
                    new Lugar { Nombre = "Playa de Frío Norte", Tipo = "Playa", Direccion = "Ruta 31 km 12" }
                );
                context.SaveChanges();
                }

            if (!context.LugaresOrigen.Any())
                {
                context.LugaresOrigen.AddRange(
                    new LugarOrigen { Nombre = "Quinta La Esperanza", Direccion = "Ruta 5 km 120", Localidad = "Salto" },
                    new LugarOrigen { Nombre = "Finca San José", Direccion = "Ruta 8 km 45", Localidad = "Paysandú" },
                    new LugarOrigen { Nombre = "Campo del Este", Direccion = "Ruta 3 km 210", Localidad = "Artigas" }
                );
                context.SaveChanges();
                }

            if (!context.LugaresDestino.Any())
                {
                context.LugaresDestino.AddRange(
                    new LugarDestino { Nombre = "Frigorífico Puerto", Direccion = "Rambla Gran Bretaña s/n", Localidad = "Montevideo" },
                    new LugarDestino { Nombre = "Terminal Canelones", Direccion = "Ruta 8 km 42", Localidad = "Canelones" },
                    new LugarDestino { Nombre = "Depósito Colonia", Direccion = "Ruta 1 km 218", Localidad = "Colonia" }
                );
                context.SaveChanges();
                }

            // ProductoresChacra: nexo productor <-> chacra (Quinta).
            if (!context.ProductoresChacra.Any())
                {
                var productoresDb = context.Productores.ToList();
                var quintasDb = context.Quintas.ToList();
                if (productoresDb.Any() && quintasDb.Any())
                    {
                    var productorPorId = productoresDb.ToDictionary(p => p.Id);
                    foreach (var quinta in quintasDb)
                        {
                        if (!productorPorId.TryGetValue(quinta.ProductorId, out var productorQuinta))
                            continue;

                        // IMPORTANTE: ProductorChacra no declara propiedad FK a
                        // Productor, por lo que EF mapea una FK shadow "ProductorId"
                        // (columna real de la tabla). Si no se asigna la navegación,
                        // el insert manda ProductorId = 0 y SQL Server rechaza la
                        // fila por la FK (el error aparece como MERGE porque el
                        // lote de inserts múltiples se emite con MERGE ... OUTPUT).
                        context.ProductoresChacra.Add(new ProductorChacra
                            {
                            IdProductor = quinta.ProductorId,
                            IdChacra = quinta.Id,
                            Productor = productorQuinta,
                            Descripcion = $"{productorQuinta.Nombre} - {quinta.Nombre}"
                            });
                        }
                    context.SaveChanges();
                    }
                }
            }

        // ------------------------------------------------------------------
        // Completar Lotes (sólo campos vacíos/cero)
        // ------------------------------------------------------------------

        /// <summary>
        /// Completa los Lotes sembrados sin pisar datos existentes:
        /// coordenadas GPS (zona Salto: ~-31.30/-57.60), KilosNetos a partir de
        /// bins (~400-440 kg/bin), FKs a catálogos, IdRemito y bins
        /// despachados/envasados coherentes (saldo >= 0).
        /// </summary>
        private static void CompletarLotes(ApplicationDbContext context, Random random)
            {
            var tiposLote = context.TiposLote.AsNoTracking().Where(t => !t.IsDeleted).ToList();
            var basculas = context.Basculas.AsNoTracking().Where(b => !b.IsDeleted).ToList();
            var destinosInternos = context.DestinosInternos.AsNoTracking().Where(d => !d.IsDeleted).ToList();
            if (tiposLote.Count == 0 || basculas.Count == 0 || destinosInternos.Count == 0)
                return;

            var lotes = context.Lotes.Where(l => !l.IsDeleted).ToList();
            foreach (var lote in lotes)
                {
                var cambio = false;

                if (!lote.Latitude.HasValue)
                    {
                    lote.Latitude = Math.Round(-31.30m - (decimal)(random.NextDouble() * 0.30), 6);
                    cambio = true;
                    }
                if (!lote.Longitude.HasValue)
                    {
                    lote.Longitude = Math.Round(-57.60m - (decimal)(random.NextDouble() * 0.40), 6);
                    cambio = true;
                    }

                if (lote.KilosNetos == 0 && lote.CantidadBins > 0)
                    {
                    var kilosPorBin = 400 + random.Next(0, 41); // 400..440 kg
                    lote.KilosNetos = Math.Round(lote.CantidadBins * (decimal)kilosPorBin, 2);
                    cambio = true;
                    }

                if (!lote.TipoLoteId.HasValue)
                    {
                    lote.TipoLoteId = tiposLote[random.Next(tiposLote.Count)].Id;
                    cambio = true;
                    }
                if (!lote.BasculaId.HasValue)
                    {
                    lote.BasculaId = basculas[random.Next(basculas.Count)].Id;
                    cambio = true;
                    }
                if (!lote.DestinoInternoId.HasValue)
                    {
                    lote.DestinoInternoId = destinosInternos[random.Next(destinosInternos.Count)].Id;
                    cambio = true;
                    }

                if (string.IsNullOrWhiteSpace(lote.IdRemito))
                    {
                    var anioRemito = (lote.FechaIngreso ?? DateTime.Today).Year;
                    lote.IdRemito = $"REM-{anioRemito}-{lote.Id:00000}";
                    cambio = true;
                    }

                if (lote.CantidadBinsDesp == 0 && lote.CantidadBinsEmb == 0 && lote.CantidadBins > 0)
                    {
                    // 30-90 % despachados; el resto se reparte en envasado
                    // dejando saldo >= 0 (bins sin movimiento).
                    var pctDesp = 30 + random.Next(0, 61);
                    var despachados = lote.CantidadBins * pctDesp / 100;
                    var envasados = (lote.CantidadBins - despachados) * random.Next(0, 101) / 100;
                    lote.CantidadBinsDesp = despachados;
                    lote.CantidadBinsEmb = envasados;
                    cambio = true;
                    }

                // Auditoría sólo si hubo cambios reales: la segunda corrida del
                // seed no debe marcar UpdatedAt en lotes ya completos.
                if (cambio)
                    {
                    lote.UpdatedAt = DateTime.UtcNow;
                    lote.UpdatedBy = "DbInitializer";
                    }
                }
            }

        // ------------------------------------------------------------------
        // Completar Productores (sólo campos vacíos)
        // ------------------------------------------------------------------

        /// <summary>
        /// Completa RazonSocial ("{Nombre} S.A.") y Ruc uruguayo de estructura
        /// 80XXXXXXXX-N donde estén vacíos. El Ruc es inventado (no validado
        /// contra DGI): sólo da forma al dato para las pantallas.
        /// </summary>
        private static void CompletarProductores(ApplicationDbContext context)
            {
            var productores = context.Productores.Where(p => !p.IsDeleted).ToList();
            foreach (var productor in productores)
                {
                var cambio = false;

                if (string.IsNullOrWhiteSpace(productor.RazonSocial))
                    {
                    productor.RazonSocial = $"{productor.Nombre} S.A.";
                    cambio = true;
                    }

                if (string.IsNullOrWhiteSpace(productor.Ruc))
                    {
                    // Semilla determinística por Id para que el seed sea estable.
                    var rnd = new Random(4321 + productor.Id);
                    var digitos = new char[8];
                    for (int i = 0; i < digitos.Length; i++)
                        digitos[i] = (char)('0' + rnd.Next(0, 10));
                    productor.Ruc = $"80{new string(digitos)}-{rnd.Next(0, 10)}";
                    cambio = true;
                    }

                if (cambio)
                    {
                    productor.UpdatedAt = DateTime.UtcNow;
                    productor.UpdatedBy = "DbInitializer";
                    }
                }
            }

        // ------------------------------------------------------------------
        // LotesEmboquille (Stock Playa)
        // ------------------------------------------------------------------

        /// <summary>
        /// Siembra pesajes/emboquilles de las temporadas {año-1, año} dentro de
        /// la ventana abril 15 - octubre 31 (limitada a hoy), con tasas
        /// filas-día por especie que reproducen el pico may-sep (~980 filas).
        /// Estado "E" (en playa) para los últimos 60 días con saldo >= 0; los
        /// más antiguos quedan cerrados como "D" (despachado) o "V" (volcado).
        /// </summary>
        private static void SembrarLotesEmboquille(ApplicationDbContext context, Random random)
            {
            // Solo saltar si YA sembramos nosotros: otras siembras (p. ej. la
            // legada SeedProyeccionZafra, que escribe Estado="Finalizado") llenan
            // esta misma tabla y no deben impedir la siembra de Stock Playa
            // (filas con Estado = EstadoEmboquille.EnPlaya, es decir "E"), que de
            // lo contrario queda siempre vacía para el handler
            // Estado == EstadoEmboquille.EnPlaya de la página Stock en Playa.
            if (context.LotesEmboquille.Any(e => e.CreatedBy == "DbInitializer"))
                return;

            var basculas = context.Basculas.AsNoTracking().Where(b => !b.IsDeleted).Select(b => b.Id).ToList();
            var destinos = context.DestinosInternos.AsNoTracking().Where(d => !d.IsDeleted).Select(d => d.Id).ToList();
            var empaques = context.Empaques.AsNoTracking().Where(e => !e.IsDeleted).Select(e => e.Id).ToList();
            var emboquilles = context.Emboquilles.AsNoTracking().Where(e => !e.IsDeleted).Select(e => e.Id).ToList();
            if (basculas.Count == 0 || destinos.Count == 0 || empaques.Count == 0 || emboquilles.Count == 0)
                return;

            // Lotes por especie (vía Variedad -> EspecieId), rotativos por fila.
            var lotes = context.Lotes.AsNoTracking()
                .Include(l => l.Variedad)
                .Where(l => !l.IsDeleted)
                .AsEnumerable()
                .Where(l => l.Variedad != null && l.Variedad.EspecieId.HasValue)
                .ToList();
            if (lotes.Count == 0)
                return;

            var lotesPorEspecie = lotes
                .GroupBy(l => l.Variedad!.EspecieId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());
            var rotacion = new Dictionary<int, int>();

            var especies = context.Especies.AsNoTracking().ToList();
            int? IdEspecie(string descripcion) =>
                especies.FirstOrDefault(e => e.Descripcion == descripcion)?.Id;

            // Tasas filas-día por especie (pico: jun-agosto).
            var tasasPorEspecie = new Dictionary<int, Dictionary<int, double>>();
            void AgregarTasa(int? idEspecie, Dictionary<int, double> tasas)
                {
                if (idEspecie.HasValue && lotesPorEspecie.ContainsKey(idEspecie.Value))
                    tasasPorEspecie[idEspecie.Value] = tasas;
                }

            AgregarTasa(IdEspecie("Naranja"), new Dictionary<int, double>
                { [4] = 0.12, [5] = 0.45, [6] = 1.5, [7] = 1.9, [8] = 1.5, [9] = 0.6, [10] = 0.18 });
            AgregarTasa(IdEspecie("Mandarina"), new Dictionary<int, double>
                { [5] = 0.15, [6] = 1.0, [7] = 1.3, [8] = 1.15, [9] = 0.4, [10] = 0.12 });
            AgregarTasa(IdEspecie("Limón"), new Dictionary<int, double>
                { [4] = 0.10, [5] = 0.20, [6] = 0.50, [7] = 0.60, [8] = 0.60, [9] = 0.60, [10] = 0.50 });
            AgregarTasa(IdEspecie("Pomelo"), new Dictionary<int, double>
                { [5] = 0.25, [6] = 0.75, [7] = 0.95, [8] = 0.85, [9] = 0.35, [10] = 0.12 });
            if (tasasPorEspecie.Count == 0)
                return;

            var hoy = DateTime.Today;
            var inicio = new DateTime(hoy.Year - 1, 4, 15);
            var fin = new DateTime(hoy.Year, 10, 31);
            if (fin > hoy)
                fin = hoy;

            // Acumulador fraccional por especie: la tasa diaria va "ahorrando"
            // filas para emitir una fila entera cuando supera 1.
            var acumulador = tasasPorEspecie.Keys.ToDictionary(k => k, _ => 0d);
            var filas = new List<LoteEmboquille>();

            for (var fecha = inicio; fecha <= fin; fecha = fecha.AddDays(1))
                {
                foreach (var especieKvp in tasasPorEspecie)
                    {
                    var especieId = especieKvp.Key;
                    var tasa = especieKvp.Value.TryGetValue(fecha.Month, out var t) ? t : 0d;
                    if (tasa <= 0)
                        continue;

                    acumulador[especieId] += tasa;
                    if (acumulador[especieId] < 1.0)
                        continue;
                    acumulador[especieId] -= 1.0;

                    // Bins por fila según el mes de la temporada.
                    int bins;
                    if (fecha.Month == 6 || fecha.Month == 7 || fecha.Month == 8)
                        bins = random.Next(38, 81);      // 38..80
                    else if (fecha.Month == 5 || fecha.Month == 9)
                        bins = random.Next(22, 51);      // 22..50
                    else
                        bins = random.Next(15, 36);      // 15..35 (abr/oct)

                    var kgPorBin = 400 + random.Next(0, 41); // 400..440 kg

                    // Lote rotativo de la misma especie; se prefiere uno con
                    // CantidadBins suficiente para no excederlo.
                    var candidatos = lotesPorEspecie[especieId];
                    var indice = rotacion.TryGetValue(especieId, out var idx) ? idx : 0;
                    rotacion[especieId] = indice + 1;
                    Lote? loteElegido = null;
                    for (int intento = 0; intento < candidatos.Count; intento++)
                        {
                        var candidato = candidatos[(indice + intento) % candidatos.Count];
                        if (candidato.CantidadBins >= bins)
                            {
                            loteElegido = candidato;
                            break;
                            }
                        }
                    loteElegido ??= candidatos[indice % candidatos.Count];

                    // Estado: en playa (saldo >= 0) si el pesaje es reciente;
                    // si no, cerrado como despachado (70 %) o volcado. Los
                    // valores textuales ("E"/"D"/"V") son los heredados de
                    // DTrace y viven en la BD tal cual; el código usa las
                    // constantes para que cada rama diga qué representa.
                    string estado;
                    int binsDespachados = 0;
                    int binsVolcados = 0;
                    if (fecha >= hoy.AddDays(-60))
                        {
                        estado = EstadoEmboquille.EnPlaya;
                        var consumidos = random.Next(0, bins); // saldo >= 0 siempre
                        if (random.Next(100) < 60)
                            binsDespachados = consumidos;
                        else
                            binsVolcados = consumidos;
                        }
                    else if (random.Next(100) < 70)
                        {
                        estado = EstadoEmboquille.Despachado;
                        binsDespachados = bins;
                        }
                    else
                        {
                        estado = EstadoEmboquille.Volcado;
                        binsVolcados = bins;
                        }

                    filas.Add(new LoteEmboquille
                        {
                        LoteId = loteElegido.Id,
                        EmboquilleId = emboquilles[random.Next(emboquilles.Count)],
                        FechaProduccion = fecha.AddHours(random.Next(6, 18)),
                        KilosNetos = Math.Round(bins * (decimal)kgPorBin, 2),
                        CantidadBins = bins,
                        Zafra = fecha.Year.ToString(),
                        IdBascula = basculas[random.Next(basculas.Count)],
                        IdDestinoInterno = destinos[random.Next(destinos.Count)],
                        IdEmpaque = empaques[random.Next(empaques.Count)],
                        CantidadBinsDespachados = binsDespachados,
                        CantidadBinsVolcados = binsVolcados,
                        Estado = estado,
                        CreatedAt = fecha,
                        CreatedBy = "DbInitializer",
                        IsDeleted = false
                        });
                    }
                }

            context.LotesEmboquille.AddRange(filas);
            context.SaveChanges();
            }

        // ------------------------------------------------------------------
        // Pallets: completar campos de producción/estado
        // ------------------------------------------------------------------

        /// <summary>
        /// Completa los pallets sembrados por DbInitializer (o sin autor):
        /// Produccion ("Zafra {año}"), PesoNeto (envases × kg del envase) y
        /// Estado mixto por antigüedad para que "Pallets en Frío" muestre datos
        /// reales (EnFrio/PreFrio junto a Despachado/Rearmado/Bajado).
        /// Idempotente: sólo toca pallets con Produccion vacío o PesoNeto NULL.
        /// </summary>
        private static void CompletarPallets(ApplicationDbContext context, Random random)
            {
            var lotesCodigos = context.Lotes.AsNoTracking()
                .Where(l => !l.IsDeleted)
                .Select(l => l.Codigo)
                .ToList();

            var pallets = context.Pallets
                .Where(p => !p.IsDeleted &&
                            (p.CreatedBy == null || p.CreatedBy == "DbInitializer") &&
                            (p.Produccion == null || p.Produccion == "" || p.PesoNeto == null))
                .ToList();

            var hoy = DateTime.Today;
            foreach (var pallet in pallets)
                {
                var fechaPallet = (pallet.Fecha ?? pallet.CreatedAt).Date;
                var dias = (hoy - fechaPallet).Days;

                if (string.IsNullOrWhiteSpace(pallet.Produccion))
                    pallet.Produccion = $"Zafra {fechaPallet.Year}";

                if (pallet.PesoNeto == null && pallet.CantidadEnvases.HasValue)
                    pallet.PesoNeto = Math.Round(pallet.CantidadEnvases.Value * KilosPorEnvase(pallet.Envase), 2);

                // Estado mixto según antigüedad (sólo pallets de esta semilla).
                pallet.Estado = EstadoMixtoPorAntiguedad(dias, random);

                if (string.IsNullOrWhiteSpace(pallet.Frio))
                    pallet.Frio = pallet.Estado == EstadoPallet.EnFrio || pallet.Estado == EstadoPallet.PreFrio ? "Si" : "No";

                if (string.IsNullOrWhiteSpace(pallet.Especie) && !string.IsNullOrWhiteSpace(pallet.Variedad))
                    pallet.Especie = EspecieDeVariedad(pallet.Variedad);

                if (pallet.SemanaProd == null)
                    {
                    try
                        {
                        pallet.SemanaProd = ISOWeek.GetWeekOfYear(fechaPallet);
                        }
                    catch
                        {
                        // Fecha fuera de rango ISO: se omite el dato.
                        }
                    }

                if (pallet.Fecha == null)
                    pallet.Fecha = fechaPallet;

                if (string.IsNullOrWhiteSpace(pallet.Hora))
                    pallet.Hora = $"{random.Next(6, 18):00}:{random.Next(0, 60):00}";

                if (string.IsNullOrWhiteSpace(pallet.Lote) && lotesCodigos.Count > 0)
                    pallet.Lote = lotesCodigos[random.Next(lotesCodigos.Count)];

                if (string.IsNullOrWhiteSpace(pallet.TipoPallet))
                    pallet.TipoPallet = "Definitivo";

                if (string.IsNullOrWhiteSpace(pallet.Embalaje))
                    pallet.Embalaje = "Caja Cartón";

                if (string.IsNullOrWhiteSpace(pallet.FormaEmbalado))
                    pallet.FormaEmbalado = random.Next(100) < 50 ? "Red" : "Plástico";

                if (string.IsNullOrWhiteSpace(pallet.Cuadros))
                    pallet.Cuadros = (90 + random.Next(0, 7) * 10).ToString();

                if (string.IsNullOrWhiteSpace(pallet.SPA))
                    pallet.SPA = $"SPA-{random.Next(100, 1000)}";

                pallet.UpdatedAt = DateTime.UtcNow;
                pallet.UpdatedBy = "DbInitializer";
                }

            context.SaveChanges();
            }

        private static decimal KilosPorEnvase(string? envase)
            {
            if (string.IsNullOrWhiteSpace(envase))
                return 15m;

            var match = Regex.Match(envase, @"(\d+(?:[.,]\d+)?)\s*kg", RegexOptions.IgnoreCase);
            if (match.Success &&
                decimal.TryParse(match.Groups[1].Value.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var kg) &&
                kg > 0)
                return kg;

            return 15m; // bandejas/envases sin kg declarados
            }

        private static EstadoPallet EstadoMixtoPorAntiguedad(int dias, Random random)
            {
            var r = random.Next(100);
            if (dias <= 20)
                {
                // Recientes: predominan los que están en cámara de frío.
                if (r < 50) return EstadoPallet.EnFrio;
                if (r < 75) return EstadoPallet.PreFrio;
                if (r < 90) return EstadoPallet.Rearmado;
                return EstadoPallet.Despachado;
                }
            if (dias <= 45)
                {
                if (r < 55) return EstadoPallet.Despachado;
                if (r < 85) return EstadoPallet.EnFrio;
                return EstadoPallet.Rearmado;
                }

            // Antiguos: casi todos despachados.
            if (r < 85) return EstadoPallet.Despachado;
            if (r < 95) return EstadoPallet.EnFrio;
            return EstadoPallet.Bajado;
            }

        private static string? EspecieDeVariedad(string variedad)
            {
            var v = variedad.Trim();
            if (v.StartsWith("Valencia", StringComparison.OrdinalIgnoreCase) ||
                v.StartsWith("Navel", StringComparison.OrdinalIgnoreCase))
                return "Naranja";
            if (v.StartsWith("Satsuma", StringComparison.OrdinalIgnoreCase) ||
                v.StartsWith("Murcott", StringComparison.OrdinalIgnoreCase))
                return "Mandarina";
            if (v.StartsWith("Eureka", StringComparison.OrdinalIgnoreCase) ||
                v.StartsWith("Lisboa", StringComparison.OrdinalIgnoreCase))
                return "Limón";
            if (v.StartsWith("Marsh", StringComparison.OrdinalIgnoreCase) ||
                v.StartsWith("Star Ruby", StringComparison.OrdinalIgnoreCase))
                return "Pomelo";
            return null;
            }

        // ------------------------------------------------------------------
        // Eventos climáticos
        // ------------------------------------------------------------------
        // (Acá antes se sembraba una tanda de eventos climáticos inventados con
        // fechas al azar. Se eliminó: los eventos son datos medidos por el
        // escáner automático de Open-Meteo; una siembra artificial distorsionaba
        // los KPIs financieros y confundía al usuario, que veía "impactos ya
        // ocurridos" que nunca pasaron.)

        // ------------------------------------------------------------------
        // vRitmoCalibradora (Gráfica de Procesos)
        // ------------------------------------------------------------------

        /// <summary>
        /// Crea la tabla [dbo].[vRitmoCalibradora] si no existe (la entidad está
        /// mapeada con ToView, por lo que las migraciones no la crean) y la
        /// siembra coherente con LotesEmboquille: calibradora = kilos procesados
        /// del día por especie, ritmo1 = calibradora/hscal y ritmo2 =
        /// calibradora/(hscal+hspar), las fórmulas que usa GraficaProcesos.razor.
        /// Se inserta por ADO.NET porque la entidad es keyless (no acepta Add).
        /// </summary>
        private static void SembrarRitmosCalibradora(ApplicationDbContext context, Random random)
            {
            context.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'dbo.vRitmoCalibradora') IS NULL
    CREATE TABLE [dbo].[vRitmoCalibradora](
        [Loteroda]      [nvarchar](50)  NULL,
        [fecha]         [datetime2](7)  NOT NULL,
        [idespecie]     [int]           NULL,
        [especie]       [nvarchar](100) NULL,
        [variedad]      [nvarchar](100) NULL,
        [idvariedad]    [int]           NULL,
        [calibradora]   [int]           NULL,
        [dcte_sincera]  [int]           NULL,
        [volcado]       [int]           NULL,
        [hscal]         [decimal](10,2) NULL,
        [hspar]         [decimal](10,2) NULL,
        [ritmo1]        [int]           NULL,
        [ritmo2]        [int]           NULL,
        [observacion]   [nvarchar](200) NULL
    );");

            if (context.RitmosCalibradora.Any())
                return;

            // Filas por (día, especie) de los últimos 90 días de pesajes
            // (rango por defecto de GraficaProcesos: últimos 3 meses).
            var desde = DateTime.Today.AddDays(-90);
            var pesajes = context.LotesEmboquille.AsNoTracking()
                .Include(le => le.Lote).ThenInclude(l => l.Variedad).ThenInclude(v => v!.Especie)
                .Where(le => !le.IsDeleted && le.FechaProduccion >= desde)
                .AsEnumerable()
                .Where(le => le.Lote?.Variedad?.EspecieId.HasValue == true)
                .ToList();

            if (pesajes.Count == 0)
                {
                // Respaldo: usar todos los pesajes si no hay filas en los últimos 90 días.
                pesajes = context.LotesEmboquille.AsNoTracking()
                    .Include(le => le.Lote).ThenInclude(l => l.Variedad).ThenInclude(v => v!.Especie)
                    .Where(le => !le.IsDeleted)
                    .AsEnumerable()
                    .Where(le => le.Lote?.Variedad?.EspecieId.HasValue == true)
                    .ToList();
                }
            if (pesajes.Count == 0)
                return;

            var grupos = pesajes
                .GroupBy(le => new
                    {
                    Dia = le.FechaProduccion.Date,
                    EspecieId = le.Lote!.Variedad!.EspecieId!.Value
                    })
                .OrderBy(g => g.Key.Dia)
                .ToList();

            var filas = new List<RitmoCalibradora>();
            foreach (var grupo in grupos)
                {
                var kilosDelDia = (int)Math.Round(grupo.Sum(le => le.KilosNetos));
                if (kilosDelDia <= 0)
                    continue;

                var variedad = grupo.First().Lote!.Variedad!;
                var hscal = Math.Round((decimal)(8 + random.NextDouble() * 6), 2);     // 8..14 h calibradora
                var hspar = Math.Round((decimal)(0.5 + random.NextDouble() * 2.5), 2); // 0.5..3 h paradas
                var ritmo1 = (int)Math.Round(kilosDelDia / hscal);                     // kg/h con paradas fuera
                var ritmo2 = (int)Math.Round(kilosDelDia / (hscal + hspar));           // kg/h con paradas incluidas

                filas.Add(new RitmoCalibradora
                    {
                    Loteroda = grupo.First().Lote!.Codigo,
                    Fecha = grupo.Key.Dia,
                    IdEspecie = variedad.EspecieId,
                    Especie = variedad.Especie?.Descripcion,
                    Variedad = variedad.Nombre,
                    IdVariedad = variedad.Id,
                    Calibradora = kilosDelDia,
                    DcteSincera = random.Next(20, 121),
                    Volcado = (int)Math.Round(kilosDelDia * (0.9 + random.NextDouble() * 0.2)),
                    Hscal = hscal,
                    Hspar = hspar,
                    Ritmo1 = ritmo1,
                    Ritmo2 = ritmo2,
                    Observacion = random.Next(100) < 15 ? "Paradas por ajuste de línea" : null
                    });
                }

            if (filas.Count == 0)
                return;

            // Entidad keyless: no pasa por el ChangeTracker. Insert crudo sobre
            // la misma conexión del contexto.
            var connection = context.Database.GetDbConnection();
            var cerrarConexion = connection.State != ConnectionState.Open;
            if (cerrarConexion)
                connection.Open();

            try
                {
                using var command = connection.CreateCommand();
                command.CommandText = @"
INSERT INTO [dbo].[vRitmoCalibradora]
    (Loteroda, fecha, idespecie, especie, variedad, idvariedad,
     calibradora, dcte_sincera, volcado, hscal, hspar, ritmo1, ritmo2, observacion)
VALUES
    (@Loteroda, @fecha, @idespecie, @especie, @variedad, @idvariedad,
     @calibradora, @dcte_sincera, @volcado, @hscal, @hspar, @ritmo1, @ritmo2, @observacion);";

                var pLoteroda = command.CreateParameter(); pLoteroda.ParameterName = "@Loteroda"; pLoteroda.DbType = DbType.String; pLoteroda.Size = 50; command.Parameters.Add(pLoteroda);
                var pFecha = command.CreateParameter(); pFecha.ParameterName = "@fecha"; pFecha.DbType = DbType.DateTime2; command.Parameters.Add(pFecha);
                var pIdEspecie = command.CreateParameter(); pIdEspecie.ParameterName = "@idespecie"; pIdEspecie.DbType = DbType.Int32; command.Parameters.Add(pIdEspecie);
                var pEspecie = command.CreateParameter(); pEspecie.ParameterName = "@especie"; pEspecie.DbType = DbType.String; pEspecie.Size = 100; command.Parameters.Add(pEspecie);
                var pVariedad = command.CreateParameter(); pVariedad.ParameterName = "@variedad"; pVariedad.DbType = DbType.String; pVariedad.Size = 100; command.Parameters.Add(pVariedad);
                var pIdVariedad = command.CreateParameter(); pIdVariedad.ParameterName = "@idvariedad"; pIdVariedad.DbType = DbType.Int32; command.Parameters.Add(pIdVariedad);
                var pCalibradora = command.CreateParameter(); pCalibradora.ParameterName = "@calibradora"; pCalibradora.DbType = DbType.Int32; command.Parameters.Add(pCalibradora);
                var pDcte = command.CreateParameter(); pDcte.ParameterName = "@dcte_sincera"; pDcte.DbType = DbType.Int32; command.Parameters.Add(pDcte);
                var pVolcado = command.CreateParameter(); pVolcado.ParameterName = "@volcado"; pVolcado.DbType = DbType.Int32; command.Parameters.Add(pVolcado);
                var pHscal = command.CreateParameter(); pHscal.ParameterName = "@hscal"; pHscal.DbType = DbType.Decimal; command.Parameters.Add(pHscal);
                var pHspar = command.CreateParameter(); pHspar.ParameterName = "@hspar"; pHspar.DbType = DbType.Decimal; command.Parameters.Add(pHspar);
                var pRitmo1 = command.CreateParameter(); pRitmo1.ParameterName = "@ritmo1"; pRitmo1.DbType = DbType.Int32; command.Parameters.Add(pRitmo1);
                var pRitmo2 = command.CreateParameter(); pRitmo2.ParameterName = "@ritmo2"; pRitmo2.DbType = DbType.Int32; command.Parameters.Add(pRitmo2);
                var pObservacion = command.CreateParameter(); pObservacion.ParameterName = "@observacion"; pObservacion.DbType = DbType.String; pObservacion.Size = 200; command.Parameters.Add(pObservacion);

                foreach (var fila in filas)
                    {
                    pLoteroda.Value = (object?)fila.Loteroda ?? DBNull.Value;
                    pFecha.Value = fila.Fecha;
                    pIdEspecie.Value = (object?)fila.IdEspecie ?? DBNull.Value;
                    pEspecie.Value = (object?)fila.Especie ?? DBNull.Value;
                    pVariedad.Value = (object?)fila.Variedad ?? DBNull.Value;
                    pIdVariedad.Value = (object?)fila.IdVariedad ?? DBNull.Value;
                    pCalibradora.Value = (object?)fila.Calibradora ?? DBNull.Value;
                    pDcte.Value = (object?)fila.DcteSincera ?? DBNull.Value;
                    pVolcado.Value = (object?)fila.Volcado ?? DBNull.Value;
                    pHscal.Value = (object?)fila.Hscal ?? DBNull.Value;
                    pHspar.Value = (object?)fila.Hspar ?? DBNull.Value;
                    pRitmo1.Value = (object?)fila.Ritmo1 ?? DBNull.Value;
                    pRitmo2.Value = (object?)fila.Ritmo2 ?? DBNull.Value;
                    pObservacion.Value = (object?)fila.Observacion ?? DBNull.Value;

                    command.ExecuteNonQuery();
                    }
                }
            finally
                {
                if (cerrarConexion)
                    connection.Close();
                }
            }

        // ------------------------------------------------------------------
        // Usuarios por rol y auditoría
        // ------------------------------------------------------------------

        /// <summary>
        /// Siembra usuarios de respaldo gerencia/contador/operador con la
        /// contraseña "Guarino2026" hasheada con el PasswordHasher existente
        /// (PBKDF2-SHA256, nunca texto plano). Sólo si ya existe al menos un
        /// usuario: se preserva FR-8 (el primer usuario creado manualmente es
        /// Admin) y el usuario admin existente jamás se modifica.
        /// </summary>
        private static void SembrarUsuariosDeRol(ApplicationDbContext context)
            {
            if (!context.Users.Any())
                return;

            var roles = context.Roles.AsNoTracking().Where(r => !r.IsDeleted && r.IsActive).ToList();
            // Collation CI en la base: el chequeo debe ser case-insensitive para
            // no chocar con el índice único de UserName.
            var existentes = context.Users.AsNoTracking()
                .Select(u => u.UserName)
                .AsEnumerable()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var hasher = new PasswordHasher();

            var candidatos = new (string UserName, string FullName, string Rol)[]
                {
                ("gerencia", "Gerencia General", "Gerencia"),
                ("contador", "Contaduría General", "Contador"),
                ("operador", "Operador de Planta", "Operador")
                };

            foreach (var (userName, fullName, nombreRol) in candidatos)
                {
                if (existentes.Contains(userName))
                    continue;

                var rol = roles.FirstOrDefault(r => r.Name == nombreRol);
                if (rol == null)
                    continue;

                context.Users.Add(new AppUser
                    {
                    UserName = userName,
                    FullName = fullName,
                    PasswordHash = hasher.Hash("Guarino2026"),
                    RoleId = rol.Id,
                    IsActive = true,
                    ActivatedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "DbInitializer",
                    IsDeleted = false
                    });
                }

            context.SaveChanges();
            }

        /// <summary>
        /// Completa el correo de los usuarios con rol Admin que todavía no
        /// tengan (la columna Email se agregó después de la primera siembra,
        /// por lo que las bases existentes quedan con NULL). Idempotente: no
        /// pisa correos ya cargados ni toca a los usuarios que no son Admin.
        /// </summary>
        private static void AsegurarEmailAdmin(ApplicationDbContext context, string? emailAdmin)
            {
            if (string.IsNullOrWhiteSpace(emailAdmin))
                return;

            var adminsSinCorreo = context.Users
                .Where(u => !u.IsDeleted
                    && (u.Email == null || u.Email == "")
                    && u.Role != null && u.Role.Name == "Admin")
                .ToList();

            foreach (var admin in adminsSinCorreo)
                admin.Email = emailAdmin;

            context.SaveChanges();
            }

        /// <summary>
        /// Siembra ~48 LogEntries variados (View/Export/Create/Update/Delete/
        /// Login/LoginFailed/Consulta IA/McpCall/Error) con timestamps
        /// decrecientes. Sin datos sensibles: AdditionalData queda NULL y las
        /// contraseñas jamás se registran.
        /// </summary>
        private static void SembrarLogEntries(ApplicationDbContext context, Random random)
            {
            if (context.LogEntries.Any())
                return;

            var usuarios = context.Users.AsNoTracking()
                .Select(u => new { u.UserName, Rol = u.Role != null ? u.Role.Name : "Admin" })
                .ToList();
            if (usuarios.Count == 0)
                usuarios =
                    [
                    new { UserName = "admin", Rol = "Admin" }
                    ];

            var acciones = new[]
                {
                "View", "Export", "Create", "Update", "Delete",
                "Login", "LoginFailed", "Consulta IA", "McpCall", "Error"
                };
            var modulos = new[]
                {
                "Acceso", "Usuarios", "Stock Playa", "Auditoría", "Asistente IA",
                "Gerencia", "Registro de Pesadas", "Contaduría", "Proyección de Zafra",
                "Riesgo Climático", "Gráfica de Procesos", "Pallets"
                };

            var logs = new List<LogEntry>();
            for (int i = 0; i < 48; i++)
                {
                var usuario = usuarios[random.Next(usuarios.Count)];
                var accion = acciones[random.Next(acciones.Length)];
                var modulo = modulos[random.Next(modulos.Length)];

                var descripcion = accion switch
                    {
                    "Login" => "Inicio de sesión exitoso.",
                    "LoginFailed" => "Intento de inicio de sesión fallido (usuario o contraseña incorrectos).",
                    "Consulta IA" => "Realizó una consulta al asistente IA.",
                    "McpCall" => "Invocó una herramienta MCP de datos.",
                    "Error" => $"Error interno procesando una solicitud del módulo {modulo}.",
                    "Export" => $"Exportó datos del módulo {modulo} a Excel.",
                    "Create" => $"Creó un registro en {modulo}.",
                    "Update" => $"Actualizó un registro en {modulo}.",
                    "Delete" => $"Eliminó (borrado lógico) un registro en {modulo}.",
                    _ => $"Consultó el módulo {modulo}."
                    };

                logs.Add(new LogEntry
                    {
                    UserName = usuario.UserName,
                    Role = usuario.Rol,
                    Action = accion,
                    Module = modulo,
                    Description = descripcion,
                    // Entre 5 minutos y ~21 días hacia atrás.
                    Timestamp = DateTime.UtcNow.AddMinutes(-random.Next(5, 30241)),
                    IpAddress = $"10.0.0.{random.Next(2, 254)}",
                    AdditionalData = null,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = usuario.UserName,
                    IsDeleted = false
                    });
                }

            context.LogEntries.AddRange(logs);
            context.SaveChanges();
            }
        }
    }

using GuarinoApp.Domain.Entities;
using GuarinoApp.Domain.Enums;

namespace GuarinoApp.Infrastructure.Data
    {
    public static class DbInitializer
        {
        public static void Initialize(ApplicationDbContext context)
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

            // 5. Seed Lotes (Con FKs válidas)
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
                }

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
                        EstadoTexto = "F",
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

            // Save final solo para datos dependientes
            context.SaveChanges();
            }
        }
    }

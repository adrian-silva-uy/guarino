using QuestPDF.Fluent;
using QuestPDF.Helpers;

namespace GuarinoApp.Infrastructure.Services
    {
    public class QuestPdfGenerator : IPdfGenerator
        {
        public Task<byte[]> GeneratePalletPdfAsync(CancellationToken cancellationToken = default)
            {
            // QuestPDF es CPU puro: se ejecuta fuera del hilo del circuito para
            // no congelar la pantalla del usuario.
            return Task.Run(() =>
            {
            var bytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(20);
                    page.DefaultTextStyle(x => x.FontSize(12));

                    page.Header()
                        .Text("Pallet definitivo")
                        .SemiBold()
                        .FontSize(18)
                        .AlignCenter();

                    page.Content().PaddingVertical(10).Column(column =>
                    {
                        column.Spacing(6);
                        column.Item().Text($"Fecha: {DateTime.Now:G}");
                        column.Item().Text("Detalle del pallet:").Bold();
                        column.Item().Text("• Código: PALLET-123");
                        column.Item().Text("• Peso: 1200 kg");
                        column.Item().Text("• Contenido: Fruta variada");
                        column.Item().PaddingTop(10).Text("Observaciones:").Italic().FontSize(10);
                        column.Item().Text("Este documento ha sido generado automáticamente por GuarinoApp.");
                    });

                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("GuarinoApp");
                            x.Span(" • ").FontColor(Colors.Grey.Medium);
                            x.Span(DateTime.Now.ToString("g")).FontColor(Colors.Grey.Medium);
                        });
                });
            }).GeneratePdf();
            return bytes;
            });
            }

        public Task<byte[]> GenerateDashboardPdfAsync(CancellationToken cancellationToken = default)
            {
            // QuestPDF es CPU puro: se ejecuta fuera del hilo del circuito.
            return Task.Run(() =>
            {
            var bytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(18);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header().Text("Dashboard - Export").FontSize(16).SemiBold().AlignCenter();

                    page.Content().Column(column =>
                    {
                        column.Spacing(8);
                        column.Item().Text($"Exportado: {DateTime.Now:G}");
                        column.Item().Text("Resumen de métricas:").Bold();
                        column.Item().Row(row =>
                        {
                            row.RelativeItem().Column(col =>
                            {
                                col.Item().Text("• Total pedidos: 1.234");
                                col.Item().Text("• Pedidos pendientes: 12");
                            });
                            row.ConstantItem(10);
                            row.RelativeItem().Column(col =>
                            {
                                col.Item().Text("• Rendimiento: 98.7%");
                                col.Item().Text("• Errores: 3");
                            });
                        });
                    });

                    page.Footer().AlignCenter().Text("GuarinoApp - Dashboard Export");
                });
            }).GeneratePdf();
            return bytes;
            });
            }

        public Task<byte[]> GenerateFrutaPdfAsync(CancellationToken cancellationToken = default)
            {
            // QuestPDF es CPU puro: se ejecuta fuera del hilo del circuito.
            return Task.Run(() =>
            {
            var bytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(18);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header().Text("Export Fruta").FontSize(16).SemiBold().AlignCenter();

                    page.Content().Column(column =>
                    {
                        column.Spacing(6);
                        column.Item().Text($"Fecha: {DateTime.Now:G}");
                        column.Item().Text("Listado de partidas:");
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                            });

                            table.Header(header =>
                            {
                                header.Cell().Text("Lote").Bold();
                                header.Cell().Text("Variedad").Bold();
                                header.Cell().Text("Cantidad").Bold();
                            });

                            // Ejemplo de filas; reemplaza con datos reales
                            table.Cell().Text("L001");
                            table.Cell().Text("Manzana");
                            table.Cell().Text("200 kg");

                            table.Cell().Text("L002");
                            table.Cell().Text("Pera");
                            table.Cell().Text("150 kg");
                        });
                    });

                    page.Footer().AlignCenter().Text("GuarinoApp - Export Fruta");
                });
            }).GeneratePdf();
            return bytes;
            });
            }
        }
    }

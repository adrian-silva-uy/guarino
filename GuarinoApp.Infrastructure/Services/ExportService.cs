using ClosedXML.Excel;
using GuarinoApp.Application.Common.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;

namespace GuarinoApp.Infrastructure.Services
    {
    /// <summary>Genera archivos. La descarga corresponde exclusivamente a la capa Web.</summary>
    public sealed class ExportService : IExportService
        {
        public Task<byte[]> ExportToExcelAsync<T>(IEnumerable<T> data, string sheetName)
            {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add(string.IsNullOrWhiteSpace(sheetName) ? "Datos" : sheetName);
            var properties = typeof(T).GetProperties();

            for (var column = 0; column < properties.Length; column++)
                {
                worksheet.Cell(1, column + 1).Value = properties[column].Name;
                worksheet.Cell(1, column + 1).Style.Font.Bold = true;
                }

            var row = 2;
            foreach (var item in data)
                {
                for (var column = 0; column < properties.Length; column++)
                    worksheet.Cell(row, column + 1).Value = properties[column].GetValue(item)?.ToString() ?? string.Empty;
                row++;
                }

            worksheet.Columns().AdjustToContents();
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return Task.FromResult(stream.ToArray());
            }

        public Task<byte[]> ExportToPdfAsync<T>(IEnumerable<T> data, string title)
            {
            var properties = typeof(T).GetProperties();
            var rows = data.ToList();

            var bytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    // Horizontal: los reportes anchos (p. ej. 17 columnas) no
                    // entran en A4 vertical y las columnas se pisaban entre sí.
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(15);
                    page.DefaultTextStyle(x => x.FontSize(9));

                    page.Header().PaddingBottom(6).Column(header =>
                    {
                        header.Item().Text(title).SemiBold().FontSize(14).AlignCenter();
                        header.Item().Text($"Generado el {DateTime.Now:g} - GuarinoApp")
                              .FontSize(8).FontColor(Colors.Grey.Darken1).AlignCenter();
                    });

                    // Tabla real de QuestPDF: cada celda tiene su ancho y su
                    // texto; si no entra, hace salto de línea dentro de la
                    // celda (nunca se solapa) y el encabezado se repite en
                    // cada página.
                    page.Content().PaddingVertical(4).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            foreach (var prop in properties)
                                columns.RelativeColumn(EsNumerico(prop.PropertyType) ? 1.15f : 1.7f);
                        });

                        table.Header(header =>
                        {
                            foreach (var prop in properties)
                                {
                                header.Cell()
                                      .Background(Colors.Grey.Lighten3)
                                      .BorderBottom(1)
                                      .BorderColor(Colors.Grey.Medium)
                                      .Padding(3)
                                      .Text(prop.Name)
                                      .Bold()
                                      .FontSize(8.5f);
                                }
                        });

                        var numeroFila = 0;
                        foreach (var item in rows)
                            {
                            var fondo = numeroFila % 2 == 1 ? Colors.Grey.Lighten4 : Colors.White;
                            foreach (var prop in properties)
                                {
                                var valor = prop.GetValue(item)?.ToString() ?? string.Empty;
                                var celda = table.Cell()
                                    .Background(fondo)
                                    .BorderBottom(0.5f)
                                    .BorderColor(Colors.Grey.Lighten2)
                                    .Padding(3);
                                var texto = celda.Text(valor).FontSize(8.5f);
                                if (EsNumerico(prop.PropertyType))
                                    texto = texto.AlignRight();
                                }
                            numeroFila++;
                            }
                    });

                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.Span("Página ").FontSize(8);
                        text.CurrentPageNumber().FontSize(8);
                        text.Span(" de ").FontSize(8);
                        text.TotalPages().FontSize(8);
                    });
                });
            }).GeneratePdf();

            return Task.FromResult(bytes);
            }

        public string ExportToCsv<T>(IEnumerable<T> data)
            {
            var properties = typeof(T).GetProperties();
            var lines = new List<string> { string.Join(",", properties.Select(property => Escape(property.Name))) };
            lines.AddRange(data.Select(item => string.Join(",", properties.Select(property => Escape(property.GetValue(item)?.ToString() ?? string.Empty)))));
            return string.Join(Environment.NewLine, lines);
            }

        /// <summary>Distingue columnas numéricas (alineadas a la derecha en el PDF).</summary>
        private static bool EsNumerico(Type t)
            {
            var tipo = Nullable.GetUnderlyingType(t) ?? t;
            return tipo == typeof(short) || tipo == typeof(int) || tipo == typeof(long) ||
                   tipo == typeof(ushort) || tipo == typeof(uint) || tipo == typeof(ulong) ||
                   tipo == typeof(decimal) || tipo == typeof(double) || tipo == typeof(float);
            }

        private static string Escape(string value) =>
            value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
                ? $"\"{value.Replace("\"", "\"\"")}\""
                : value;
        }
    }

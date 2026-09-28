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
                    page.Size(PageSizes.A4);
                    page.Margin(20);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Text(title).SemiBold().FontSize(14).AlignCenter();

                    page.Content().PaddingVertical(10).Column(column =>
                    {
                        // Cabecera
                        column.Item().Row(row =>
                        {
                            foreach (var prop in properties)
                                {
                                row.RelativeItem().Text(prop.Name).Bold().FontSize(9);
                                }
                        });

                        column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                        // Filas
                        foreach (var item in rows)
                            {
                            column.Item().Row(r =>
                            {
                                foreach (var prop in properties)
                                    {
                                    var value = prop.GetValue(item)?.ToString() ?? string.Empty;
                                    r.RelativeItem().Text(value).FontSize(9);
                                    }
                            });
                            }
                    });

                    page.Footer().AlignCenter().Text(x => x.Span($"GuarinoApp • {DateTime.Now:G}").FontSize(9));
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

        private static string Escape(string value) =>
            value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
                ? $"\"{value.Replace("\"", "\"\"")}\""
                : value;
        }
    }

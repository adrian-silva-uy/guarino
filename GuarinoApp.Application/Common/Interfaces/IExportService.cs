namespace GuarinoApp.Application.Common.Interfaces
    {
    public interface IExportService
        {
        Task<byte[]> ExportToExcelAsync<T>(IEnumerable<T> data, string sheetName);
        Task<byte[]> ExportToPdfAsync<T>(IEnumerable<T> data, string title);
        string ExportToCsv<T>(IEnumerable<T> data);
        }
    }

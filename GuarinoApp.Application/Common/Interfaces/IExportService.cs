namespace GuarinoApp.Application.Common.Interfaces
    {
    public interface IExportService
        {
        Task<byte[]> ExportToExcelAsync<T>(IEnumerable<T> data, string sheetName);
        // fontSize: tamaño de letra opcional (solo reportes muy anchos, p. ej.
        // Pallets Definitivos con 28 columnas, lo piden más chico).
        // columns: subconjunto y orden de columnas para el PDF (si no se pasa,
        // salen todas las propiedades del tipo).
        Task<byte[]> ExportToPdfAsync<T>(IEnumerable<T> data, string title, float? fontSize = null, IEnumerable<string>? columns = null);
        string ExportToCsv<T>(IEnumerable<T> data);
        }
    }

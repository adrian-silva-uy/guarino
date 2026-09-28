using Microsoft.JSInterop;

namespace GuarinoApp.Web.Helpers;

public static class FileDownloadExtensions
    {
    /// <summary>
    /// Descarga un archivo de texto (CSV, JSON, etc.) usando la función JS existente downloadFileFromText.
    /// </summary>
    public static async ValueTask DownloadFileFromTextAsync(
        this IJSRuntime js,
        string fileName,
        string content,
        string mimeType = "text/csv;charset=utf-8;")
        {
        // Concatenar el BOM UTF-8 (\ufeff) para que Excel interprete tildes y caracteres especiales
        var utf8Content = "\ufeff" + content;

        await js.InvokeVoidAsync("downloadFileFromText", fileName, utf8Content, mimeType);
        }

    /// <summary>
    /// Descarga un archivo binario usando la función JS existente downloadFileFromByteArray.
    /// </summary>
    public static async ValueTask DownloadFileFromByteArrayAsync(
        this IJSRuntime js,
        string fileName,
        byte[] bytes)
        {
        await js.InvokeVoidAsync("downloadFileFromByteArray", fileName, bytes);
        }
    }

using Microsoft.JSInterop;
using MudBlazor;

namespace GuarinoApp.Web.Helpers;

/// <summary>
/// Descarga compartida de archivos exportados: intenta el diálogo nativo
/// "Guardar como..." (File System Access API, requiere Chromium + contexto
/// seguro: HTTPS o localhost) para que el usuario elija dónde guardarlo y,
/// si el navegador/contexto no lo soporta, cae a la descarga automática en
/// la carpeta de descargas. El snackbar informa qué pasó en cada caso.
/// </summary>
public static class DescargaHelper
{
    public static async Task DescargarAsync(IJSRuntime js, ISnackbar snackbar, byte[] bytes, string fileName, string mimeType)
    {
        if (bytes == null || bytes.Length == 0) throw new InvalidOperationException("El archivo generado esta vacio.");
        var resultado = await js.InvokeAsync<string>("saveAsFromBase64", fileName, Convert.ToBase64String(bytes), mimeType);
        snackbar.Add(resultado switch
        {
            "guardado" => $"💾 {fileName} guardado donde elegiste.",
            "cancelado" => $"Guardado de {fileName} cancelado.",
            _ => $"Se descargó {fileName} a la carpeta de descargas del navegador."
        }, resultado == "cancelado" ? Severity.Normal : Severity.Success);
    }

    public static Task DescargarTextoAsync(IJSRuntime js, ISnackbar snackbar, string contenido, string fileName, string mimeType)
    {
        // BOM UTF-8 para que Excel interprete tildes y caracteres especiales.
        var utf8Bom = "﻿" + (contenido ?? string.Empty);
        return DescargarAsync(js, snackbar, System.Text.Encoding.UTF8.GetBytes(utf8Bom), fileName, mimeType);
    }
}

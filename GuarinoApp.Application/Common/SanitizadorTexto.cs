using System.Text;
using System.Text.RegularExpressions;

namespace GuarinoApp.Application.Common;

/// <summary>
/// Sanitización central del texto que ingresa por formularios y diálogos.
/// Una sola fuente de verdad para toda la app. Reglas:
/// - quita caracteres de control invisibles (ESC, \0, \r suelto, zero-width…),
/// - en campos de una sola línea elimina los saltos de línea,
/// - colapsa espacios repetidos y recorta los colgantes,
/// - neutraliza pseudoetiquetas HTML pegadas ("&lt;script&gt;", "&lt;b&gt;…"): se colapsan a
///   espacios para que nada viaje disfrazado hacia la BD, los CSV, los correos
///   ni el prompt de la IA,
/// - corta al largo máximo que indique el campo.
/// Los límites por defecto son holgados a propósito: quien valida (p. ej.
/// ContactoValidacion) sigue siendo la que informa el exceso de largo.
/// </summary>
public static class SanitizadorTexto
{
    /// <summary>Campo de una sola línea (nombre, empresa, correo, teléfono, país…).</summary>
    public static string UnaLinea(string? valor, int maxLargo = 300)
        => Base(valor, multilinea: false, maxLargo);

    /// <summary>Campo multilínea (notas): conserva los saltos de línea propios del texto.</summary>
    public static string Multilinea(string? valor, int maxLargo = 4000)
        => Base(valor, multilinea: true, maxLargo);

    private static string Base(string? valor, bool multilinea, int maxLargo)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return string.Empty;

        var sb = new StringBuilder(valor.Length);
        foreach (var ch in valor.Normalize(NormalizationForm.FormC))
        {
            if (ch == '\n')
            {
                if (multilinea)
                    sb.Append('\n');
                continue;
            }

            // \r, \t, ESC, \0 y cualquier otro control: nunca deben viajar.
            if (char.IsControl(ch))
                continue;

            sb.Append(ch);
        }

        // Pseudoetiquetas HTML/ML: "<tag>" o "</tag>" colapsan a un espacio.
        var texto = Regex.Replace(sb.ToString(), "<[^>]*>", " ");

        // Espacios/tabs repetidos → uno solo (en multilinea no se tocan los \n).
        texto = Regex.Replace(texto, multilinea ? @"[^\S\n]{2,}" : @"\s{2,}", " ").Trim();

        return texto.Length > maxLargo ? texto[..maxLargo] : texto;
    }
}

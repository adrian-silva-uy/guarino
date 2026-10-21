namespace GuarinoApp.Infrastructure.Services;

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Normalización de texto para el enrutamiento por intención del Asistente IA.
/// Fuente única: la usan el SkillRegistry (capa 2) y el FiltroConsultasIA
/// (capa 1) para que una misma consulta clasifique igual en ambas capas.
/// </summary>
internal static class TextoIA
{
    /// <summary>
    /// Normaliza texto a minúsculas y sin acentos para comparar palabras clave
    /// independientemente de la escritura del usuario.
    /// </summary>
    internal static string Normalizar(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                builder.Append(ch);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>Cuenta cuántas palabras clave del tema casan con el mensaje
    /// normalizado (palabra completa: "caja" no matchea "cajas").</summary>
    internal static int ContarCoincidencias(string normalizedMessage, IEnumerable<string> keywords)
        => keywords.Count(keyword =>
        {
            var normalizedKeyword = Normalizar(keyword);
            if (normalizedKeyword.Length == 0)
                return false;

            return Regex.IsMatch(
                normalizedMessage,
                $@"\b{Regex.Escape(normalizedKeyword)}\b",
                RegexOptions.IgnoreCase);
        });
}

using System.Text.RegularExpressions;

namespace GuarinoApp.Application.Common.Security;

/// <summary>
/// Validación y limpieza de textos de entrada — OWASP "Input Validation &amp;
/// Sanitization" (Secure Coding).
///
/// Defensa en profundidad. La defensa primaria contra SQL injection es
/// consultar siempre con parámetros parametrizados (EF Core LINQ y
/// SqlParameter con allowlist de campos; nunca SQL concatenado — así está
/// implementada toda la app). Esta capa complementaria limpia automáticamente
/// todo texto libre antes de procesarlo:
/// - elimina caracteres de control,
/// - elimina secuencias estructurales de inyección SQL (comentarios
///   <c>--</c> y <c>/* */</c>, separador de sentencias <c>;</c> y comillas de
///   identificador <c>`</c>),
/// - colapsa espacios repetidos y limita la longitud máxima.
///
/// No toca las comillas simples: son legítimas en nombres y apellidos (p. ej.
/// "O'Brien") y con consultas parametrizadas no pueden romper la consulta.
///
/// Se aplica de forma automática y central a todos los campos de texto de las
/// peticiones MediatR (queries y commands) mediante
/// <see cref="Behaviors.InputSanitizationBehavior{TRequest,TResponse}"/>.
/// </summary>
public static class InputSanitizer
{
    /// <summary>Longitud máxima por defecto de un texto de entrada.</summary>
    public const int MaxLength = 2000;

    /// <summary>Caracteres de control (sin salto de línea).</summary>
    private static readonly Regex ControlChars =
        new(@"[\u0000-\u0009\u000B-\u001F\u007F]", RegexOptions.Compiled);

    /// <summary>Secuencias estructurales de inyección SQL.</summary>
    private static readonly Regex SqlInjectionSequences =
        new(@"(--|/\*|\*/|;|`)", RegexOptions.Compiled);

    /// <summary>Espacios horizontales repetidos (conserva los saltos de línea).</summary>
    private static readonly Regex HorizontalSpaces =
        new(@"[^\S\n]+", RegexOptions.Compiled);

    /// <summary>Saltos de línea excesivos.</summary>
    private static readonly Regex NewlineRuns =
        new(@"\n{3,}", RegexOptions.Compiled);

    /// <summary>
    /// Valida y limpia un texto de entrada. Devuelve <c>null</c> cuando el
    /// original era <c>null</c> (para no confundir "sin filtro" con texto vacío).
    /// Idempotente: limpiar dos veces produce el mismo resultado.
    /// </summary>
    public static string? Sanitize(string? value, int maxLength = MaxLength)
    {
        if (value is null)
            return null;

        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var s = ControlChars.Replace(value, " ");
        s = SqlInjectionSequences.Replace(s, " ");
        s = HorizontalSpaces.Replace(s, " ");
        s = NewlineRuns.Replace(s, "\n\n");
        s = s.Trim();

        if (s.Length > maxLength)
            s = s[..maxLength].Trim();

        return s;
    }

    /// <summary>True si el texto contiene secuencias de inyección SQL
    /// (útil para auditoría o advertencias).</summary>
    public static bool ContainsSqlInjectionPattern(string? value) =>
        !string.IsNullOrEmpty(value) && SqlInjectionSequences.IsMatch(value);
}

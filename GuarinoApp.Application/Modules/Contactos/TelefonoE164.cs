namespace GuarinoApp.Application.Modules.Contactos;

/// <summary>
/// Normalización de teléfonos al formato E.164 (UIT-T Rec. E.164), la norma
/// internacional de numeración de máxima calidad: «+» y hasta 15 dígitos sin
/// separadores — p. ej. "+59899123456".
/// La validación de entrada (ContactoImportParser.EsTelefono) es flexible
/// —espacios, guiones, paréntesis, salida 00—; ESTA clase canoniza lo que se
/// guarda, en los mismos tres puntos de siempre: diálogo, handlers server-side
/// e importación.
/// Reglas:
/// - "+598 99 123 456" u "00598 99 123 456" → "+59899123456".
/// - Sin prefijo internacional, si tiene pinta de número local uruguayo
///   (celular 09X… de 9 dígitos, o fijo de 8 dígitos) se infiere +598 y se
///   quita el 0 de trunk: "099 123 456" → "+59899123456";
///   "2900 1234" → "+59829001234".
/// - Otro caso sin prefijo: se devuelven solo los dígitos, sin inventar un
///   código de país (para números extranjeros usá el prefijo + o 00).
/// Llamar DESPUÉS de validar (solo con teléfonos ya aceptados); con entrada
/// no válida devuelve el texto recortado sin tocar.
/// </summary>
public static class TelefonoE164
{
    public static string Normalizar(string valor)
    {
        var v = valor.Trim();

        var conMas = v.StartsWith('+');
        var conSalidaCeroCero = !conMas && v.StartsWith("00", StringComparison.Ordinal);
        var cuerpo = conMas ? v[1..] : conSalidaCeroCero ? v[2..] : v;
        var digitos = new string(cuerpo.Where(char.IsDigit).ToArray());

        if (digitos.Length < 8 || digitos.Length > 15)
            return v; // algo raro: no se toca (quién decide es la validación)

        if (conMas || conSalidaCeroCero)
            return "+" + digitos;

        // Local uruguayo (la app es de una empresa uruguaya): inferir +598.
        if (digitos.Length == 9 && digitos.StartsWith("09", StringComparison.Ordinal))
            return "+598" + digitos[1..];
        if (digitos.Length == 8 && digitos[0] is >= '2' and <= '9')
            return "+598" + digitos;

        return digitos;
    }
}

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using GuarinoApp.Application.Modules.Contactos.Dtos;

namespace GuarinoApp.Application.Modules.Contactos;

/// <summary>
/// Parseo de contactos importados desde archivos CSV o desde texto pegado
/// (exportaciones de chat de WhatsApp, firmas de correo o listas simples).
/// Es heurístico a propósito: prioriza capturar el contacto con los datos
/// disponibles y el importador muestra la previsualización antes de dar de alta
/// (la validación final y la deduplicación las hace ImportarContactosCommand).
/// </summary>
public static class ContactoImportParser
    {
    /// <summary>Tope de filas por parseo (el handler además valida su propio tope).</summary>
    public const int MaximoFilas = 300;

    private const string EmpresaPorDefecto = "Sin empresa";

    // "16/10/26, 20:15 - " / "10/16/23, 8:15 PM - " (prefijo de cada línea del chat).
    private static readonly Regex PrefijoFecha = new(
        @"^\d{1,2}[/-]\d{1,2}[/-]\d{2,4},\s*\d{1,2}:\d{2}(?::\d{2})?\s*(?:[AaPp]\.?[Mm]\.?)?\s*[-–—]\s+",
        RegexOptions.Compiled);

    // "20:15 - " (exportaciones sin fecha).
    private static readonly Regex PrefijoHora = new(
        @"^\d{1,2}:\d{2}(?::\d{2})?\s*(?:[AaPp]\.?[Mm]\.?)?\s*[-–—]\s+",
        RegexOptions.Compiled);

    private static readonly Regex EmailRegex = new(
        @"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}",
        RegexOptions.Compiled);

    private static readonly Regex TelefonoRegex = new(
        @"\+?\d[\d\s().-]{6,17}\d",
        RegexOptions.Compiled);

    private static readonly Regex NombreEntreCorchetes = new(
        @"^([\p{L}\s.'\-]{3,60})\s*<",
        RegexOptions.Compiled);

    /// <summary>
    /// ¿El texto pega más parecido a un CSV (con fila de encabezado) que a un
    /// chat? Lo decide la primera línea con contenido.
    /// </summary>
    public static bool PareceCsv(string contenido)
        {
        if (string.IsNullOrWhiteSpace(contenido))
            return false;

        var primera = contenido.Replace("\r", "").Split('\n')
            .Select(l => l.Trim())
            .FirstOrDefault(l => l.Length > 0);
        if (primera == null)
            return false;

        var normalizada = NormalizarTitulo(primera);
        return normalizada.Contains("nombre") || normalizada == "name";
        }

    /// <summary>Parsea un CSV con fila de encabezado (separador ; , o tab, con comillas).</summary>
    public static (List<ContactoImportarDto> Filas, string? Error) ParseCsv(string contenido)
        {
        var filas = new List<ContactoImportarDto>();
        if (string.IsNullOrWhiteSpace(contenido))
            return (filas, "No hay contenido para importar.");

        var lineas = PartirLineas(contenido);
        if (lineas.Count == 0)
            return (filas, "El archivo CSV está vacío.");

        var separador = DetectarSeparador(lineas[0]);
        var encabezado = PartirCeldas(lineas[0], separador);
        var columnas = MapearColumnas(encabezado);

        if (columnas[0] < 0)
            return (filas, "No encontré una columna de Nombre en el CSV: el encabezado debe incluir «Nombre» " +
                "(opcionales: Empresa, País, Correo, Teléfono, Qué le interesó y Notas).");

        for (var i = 1; i < lineas.Count && filas.Count < MaximoFilas; i++)
            {
            var celdas = PartirCeldas(lineas[i], separador);
            if (celdas.All(string.IsNullOrWhiteSpace))
                continue;

            string? Valor(int col)
                => col >= 0 && col < celdas.Length && !string.IsNullOrWhiteSpace(celdas[col])
                    ? celdas[col].Trim()
                    : null;

            var nombre = Valor(columnas[0]);
            if (nombre == null)
                continue; // sin nombre no hay contacto

            filas.Add(new ContactoImportarDto(
                nombre,
                Valor(columnas[1]) ?? EmpresaPorDefecto,
                Valor(columnas[2]),
                Valor(columnas[3]),
                Valor(columnas[4]),
                Valor(columnas[5]),
                Valor(columnas[6])));
            }

        if (filas.Count == 0)
            return (filas, "El CSV no tiene filas con nombre de contacto.");

        return (filas, null);
        }

    /// <summary>
    /// Parsea texto pegado: líneas de un chat de WhatsApp ("Autor: mensaje"),
    /// firmas de correo ("Nombre &lt;correo&gt;") o listas simples
    /// ("Nombre, correo, teléfono").
    /// </summary>
    public static (List<ContactoImportarDto> Filas, string? Error) ParseTexto(string contenido)
        {
        var filas = new List<ContactoImportarDto>();
        if (string.IsNullOrWhiteSpace(contenido))
            return (filas, "No hay texto para importar.");

        var vistas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var cruda in contenido.Replace("\r\n", "\n").Split('\n'))
            {
            if (filas.Count >= MaximoFilas)
                break;

            var linea = cruda.Trim();
            if (linea.Length == 0)
                continue;

            linea = PrefijoFecha.Replace(linea, string.Empty);
            linea = PrefijoHora.Replace(linea, string.Empty);
            linea = linea.Trim();
            if (linea.Length == 0 || EsMensajeSistema(linea))
                continue;

            string? nombre = null;
            string? telefono = null;
            string? email = null;

            // Formato de chat: "Autor: mensaje" (los dos puntos del autor).
            var corte = IndexOfAutor(linea);
            string? autor = null;
            var resto = linea;
            if (corte >= 0)
                {
                autor = linea[..corte].Trim();
                resto = linea[(corte + 1)..].Trim();
                }

            if (autor != null)
                {
                if (EsTelefono(autor))
                    {
                    telefono = Limitar(autor, 50);
                    nombre = autor;
                    }
                else
                    {
                    nombre = LimpiarNombre(autor);
                    }
                }

            var matchEmail = EmailRegex.Match(resto);
            if (matchEmail.Success)
                email = matchEmail.Value;

            if (telefono == null)
                {
                var telefonoTexto = PrimerTelefonoValido(resto);
                if (telefonoTexto != null)
                    telefono = Limitar(telefonoTexto, 50);
                }

            // Línea suelta (sin "Autor:"): "Juan Pérez, juan@x.com, +598...".
            if (autor == null && nombre == null)
                {
                var entreCorchetes = NombreEntreCorchetes.Match(linea);
                if (entreCorchetes.Success)
                    nombre = LimpiarNombre(entreCorchetes.Groups[1].Value);
                else
                    {
                    var candidato = QuitarValores(linea, email, telefono);
                    if (EsNombreValido(candidato))
                        nombre = LimpiarNombre(candidato);
                    }
                }

            // Nombre obligatorio: si no apareció, se usa el teléfono o el correo.
            if (nombre == null && telefono != null)
                nombre = telefono;
            if (nombre == null && email != null)
                nombre = email.Length <= 150 ? email : email[..150];
            if (nombre == null)
                continue;

            nombre = LimpiarNombre(nombre);
            if (nombre.Length == 0)
                continue;

            var clave = (email ?? string.Empty) + "|" + (telefono ?? string.Empty) + "|" + nombre.ToLowerInvariant();
            if (!vistas.Add(clave))
                continue;

            filas.Add(new ContactoImportarDto(nombre, EmpresaPorDefecto, null, email, telefono, null, null));
            }

        if (filas.Count == 0)
            return (filas, "No encontré contactos en el texto. Ejemplos de línea válida: " +
                "«María López: +598 991 234 567», «Juan Pérez, juan@empresa.uy, +598 999 888 777» " +
                "o un CSV con encabezado «Nombre;Empresa;Correo;Teléfono».");

        return (filas, null);
        }

    // --- utilidades de CSV ---

    /// <summary>Parte el contenido en registros de fila respetando comillas (permite saltos dentro de celda).</summary>
    private static List<string> PartirLineas(string contenido)
        {
        if (contenido.Length > 0 && contenido[0] == '\uFEFF')
            contenido = contenido[1..];

        var lineas = new List<string>();
        var actual = new StringBuilder();
        var enComillas = false;

        for (var i = 0; i < contenido.Length; i++)
            {
            var ch = contenido[i];

            if (ch == '"')
                {
                enComillas = !enComillas;
                actual.Append(ch);
                continue;
                }

            if (!enComillas && (ch == '\n' || ch == '\r'))
                {
                lineas.Add(actual.ToString());
                actual.Clear();
                if (ch == '\r' && i + 1 < contenido.Length && contenido[i + 1] == '\n')
                    i++;
                continue;
                }

            actual.Append(ch);
            }

        if (actual.Length > 0)
            lineas.Add(actual.ToString());

        return lineas;
        }

    /// <summary>Detecta ; , o tab contándolos fuera de comillas en la fila de encabezado (empate → ';').</summary>
    private static char DetectarSeparador(string encabezado)
        {
        var enComillas = false;
        var puntocoma = 0;
        var coma = 0;
        var tab = 0;

        foreach (var ch in encabezado)
            {
            if (ch == '"') { enComillas = !enComillas; continue; }
            if (enComillas) continue;
            if (ch == ';') puntocoma++;
            else if (ch == ',') coma++;
            else if (ch == '\t') tab++;
            }

        if (puntocoma > 0 && puntocoma >= coma && puntocoma >= tab) return ';';
        if (tab > 0 && tab > coma) return '\t';
        if (coma > 0) return ',';
        return ';';
        }

    /// <summary>Parte una fila en celdas con el separador dado, interpretando comillas.</summary>
    private static string[] PartirCeldas(string linea, char separador)
        {
        var celdas = new List<string>();
        var actual = new StringBuilder();
        var enComillas = false;

        for (var i = 0; i < linea.Length; i++)
            {
            var ch = linea[i];

            if (ch == '"')
                {
                if (enComillas && i + 1 < linea.Length && linea[i + 1] == '"')
                    {
                    actual.Append('"');
                    i++;
                    continue;
                    }
                enComillas = !enComillas;
                continue;
                }

            if (ch == separador && !enComillas)
                {
                celdas.Add(actual.ToString());
                actual.Clear();
                continue;
                }

            actual.Append(ch);
            }

        celdas.Add(actual.ToString());
        return celdas.ToArray();
        }

    // Índices fijos de columnas: 0 Nombre, 1 Empresa, 2 País, 3 Correo,
    // 4 Teléfono, 5 Qué le interesó, 6 Notas. -1 = ausente en el encabezado.
    private static int[] MapearColumnas(string[] encabezado)
        {
        var mapa = new[] { -1, -1, -1, -1, -1, -1, -1 };

        for (var i = 0; i < encabezado.Length; i++)
            {
            var h = NormalizarTitulo(encabezado[i]);
            if (h.Length == 0)
                continue;

            if (mapa[0] < 0 && (h == "name" || h.Contains("nombre") || h.Contains("apellido")))
                mapa[0] = i;
            else if (mapa[1] < 0 && (h.Contains("empresa") || h.Contains("organiza") || h.Contains("compan") || h.Contains("razon social")))
                mapa[1] = i;
            else if (mapa[2] < 0 && (h == "pais" || h == "country"))
                mapa[2] = i;
            else if (mapa[3] < 0 && (h == "email" || h == "mail" || h == "e mail" || h.Contains("correo")))
                mapa[3] = i;
            else if (mapa[4] < 0 && (h.Contains("telefon") || h == "tel" || h.Contains("movil") || h.Contains("celular") || h == "phone" || h == "whatsapp" || h == "fono"))
                mapa[4] = i;
            else if (mapa[5] < 0 && h.Contains("interes"))
                mapa[5] = i;
            else if (mapa[6] < 0 && (h.Contains("nota") || h.Contains("observacion") || h == "notes"))
                mapa[6] = i;
            }

        return mapa;
        }

    /// <summary>Minúsculas, sin acentos ni puntuación: "E-mail" → "e mail".</summary>
    private static string NormalizarTitulo(string valor)
        {
        if (string.IsNullOrWhiteSpace(valor))
            return string.Empty;

        var compuesto = valor.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(compuesto.Length);
        foreach (var ch in compuesto)
            {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                builder.Append(ch);
            }

        var limpio = builder.ToString().Normalize(NormalizationForm.FormC);
        limpio = Regex.Replace(limpio, @"[^a-z0-9\s]", " ");
        return Regex.Replace(limpio, @"\s+", " ").Trim();
        }

    // --- utilidades de texto ---

    /// <summary>Posición de los dos puntos que separan "Autor" del mensaje (0 si no hay).</summary>
    private static int IndexOfAutor(string linea)
        {
        var idx = linea.IndexOf(':');
        while (idx > 0)
            {
            if (idx <= 60)
                {
                var autor = linea[..idx].Trim();
                if (autor.Length > 0 &&
                    !autor.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
                    !autor.StartsWith("www", StringComparison.OrdinalIgnoreCase) &&
                    (autor.Any(char.IsLetter) || EsTelefono(autor)))
                    return idx;
                }
            idx = linea.IndexOf(':', idx + 1);
            }
        return -1;
        }

    /// <summary>Mensajes de sistema de WhatsApp que no son contactos.</summary>
    private static bool EsMensajeSistema(string linea)
        {
        if (linea.StartsWith('<'))
            return true;

        var l = linea.ToLowerInvariant();
        return l.Contains("omitted") || l.Contains("omitido") ||
            l.Contains("end-to-end") || l.Contains("extremo a extremo") ||
            l.Contains("añadió") || l.Contains("anadio") || l.Contains("añadiste") ||
            l.Contains("agregó a") || l.Contains("creó el grupo") ||
            l.Contains("cambió el nombre") || l.Contains("cambio el nombre") ||
            l.Contains("saliste") || l.Contains("eliminaste") ||
            l.Contains("se unió usando") || l.Contains("código de invitación") ||
            l.Contains("codigo de invitacion");
        }

    /// <summary>¿La cadena tiene pinta de número de teléfono?
    /// Acepta formatos internacionales: '+' inicial o salida 00 —
    /// "+598 9912 3456", "0054 (11) 5555-1234", "+1-305-555-0199".
    /// Reglas: 8-15 dígitos, el '+' solo al principio y después únicamente
    /// dígitos, espacios, guiones, puntos, paréntesis o barras — sin letras
    /// ni '@', así un correo u otro texto pegado acá nunca pasa.
    /// Pública porque la reutilizan ContactoDialog y ContactoValidacion.</summary>
    public static bool EsTelefono(string valor)
        {
        var v = valor.Trim();
        if (v.Length < 8 || v.Length > 30)
            return false;

        // Prefijo de país: '+' solo al inicio (o una salida 00 …); lo que
        // quede después es el número "neta", el que se cuenta.
        if (v.StartsWith('+'))
            v = v[1..].TrimStart();
        else if (v.StartsWith("00", StringComparison.Ordinal))
            v = v[2..].TrimStart();

        if (v.Any(c => !(char.IsDigit(c) || char.IsWhiteSpace(c) || c is '-' or '(' or ')' or '.' or '/')))
            return false;

        var digitos = v.Count(char.IsDigit);
        return digitos >= 8 && digitos <= 15;
        }

    /// <summary>Primer teléfono válido del texto, sin pegarse a letras (evita "Pedido 12345678").</summary>
    private static string? PrimerTelefonoValido(string texto)
        {
        foreach (Match match in TelefonoRegex.Matches(texto))
            {
            var antes = match.Index > 0 ? texto[match.Index - 1] : ' ';
            var despues = match.Index + match.Length < texto.Length
                ? texto[match.Index + match.Length]
                : ' ';

            if (char.IsLetter(antes) || char.IsLetter(despues))
                continue;

            var valor = match.Value.Trim().TrimEnd('.', ',', '-', ' ');
            var digitos = valor.Count(char.IsDigit);
            if (digitos >= 8 && digitos <= 15 && !valor.Any(char.IsLetter))
                return valor;
            }

        return null;
        }

    /// <summary>Quita el correo y el teléfono de la línea para mirar si queda un nombre.</summary>
    private static string QuitarValores(string linea, string? email, string? telefono)
        {
        var s = linea;
        if (!string.IsNullOrEmpty(email))
            s = s.Replace(email, " ");
        if (!string.IsNullOrEmpty(telefono))
            s = s.Replace(telefono, " ");

        foreach (var parte in s.Split(new[] { ',', ';', '|', '/' }, StringSplitOptions.RemoveEmptyEntries))
            {
            var p = parte.Trim();
            if (p.Length >= 2 && p.Any(char.IsLetter) && !p.Any(char.IsDigit))
                return p;
            }

        return s.Trim();
        }

    /// <summary>¿El texto parece un nombre (y no un mensaje suelto)?</summary>
    private static bool EsNombreValido(string? valor)
        {
        if (string.IsNullOrWhiteSpace(valor))
            return false;

        var c = valor.Trim();
        if (c.Length < 2 || c.Length > 60)
            return false;
        if (c.Contains('?') || c.Contains('!') || c.Contains('¿') || c.Contains('¡') || c.Contains('@'))
            return false;
        if (c.EndsWith('.'))
            return false;
        if (c.Contains("http", StringComparison.OrdinalIgnoreCase) || c.Contains("www.", StringComparison.OrdinalIgnoreCase))
            return false;
        if (!c.Any(char.IsLetter))
            return false;

        // Solo letras, espacios, puntos, apóstrofos y guiones.
        if (Regex.IsMatch(c, @"[^\p{L}\s.'\-]"))
            return false;

        var palabras = c.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (palabras.Length > 6 || palabras.Any(p => p.Length > 25))
            return false;

        return true;
        }

    /// <summary>Espacios colapsados, extremos limpios y tope de 150 (límite de validación).</summary>
    private static string LimpiarNombre(string valor)
        {
        var s = Regex.Replace(valor.Trim(), @"\s+", " ");
        s = s.Trim(' ', '\t', '*', '_', '"', '\'', '-', ':', ',', '.', ';');
        return Limitar(s, 150);
        }

    private static string Limitar(string valor, int maximo)
        {
        var s = valor.Trim();
        return s.Length <= maximo ? s : s[..maximo].Trim();
        }
    }

using System.Text;

namespace GuarinoApp.Web.Helpers;

/// <summary>
/// Países del mundo (nombres en español) para el campo País de ContactoDialog.
/// Lista CERRADA: el campo solo acepta países de acá (con alias frecuentes
/// que se canonicalizan al guardar: "Holanda" → "Países Bajos").
/// La búsqueda distingue menos: ignora mayúsculas y acentos ("uruguay",
/// "URUGUAY" o "uru" encuentran Uruguay).
/// </summary>
public static class Paises
{
    /// <summary>Nombre oficial corto en español (naciones de la ONU + Vaticano y Palestina).</summary>
    public static readonly IReadOnlyList<string> Todos =
    [
        "Afganistán", "Albania", "Alemania", "Andorra", "Angola", "Antigua y Barbuda",
        "Arabia Saudita", "Argelia", "Argentina", "Armenia", "Australia", "Austria",
        "Azerbaiyán", "Bahamas", "Bangladés", "Barbados", "Baréin", "Bélgica", "Belice",
        "Benín", "Bielorrusia", "Birmania (Myanmar)", "Bolivia", "Bosnia y Herzegovina",
        "Botsuana", "Brasil", "Brunéi", "Bulgaria", "Burkina Faso", "Burundi", "Bután",
        "Cabo Verde", "Camboya", "Camerún", "Canadá", "Chad", "Chile", "China", "Chipre",
        "Colombia", "Comoras", "Corea del Norte", "Corea del Sur", "Costa de Marfil",
        "Costa Rica", "Croacia", "Cuba", "Dinamarca", "Dominica", "Ecuador", "Egipto",
        "El Salvador", "Emiratos Árabes Unidos", "Eritrea", "Eslovaquia", "Eslovenia",
        "España", "Estados Unidos", "Estonia", "Esuatini", "Etiopía", "Filipinas",
        "Finlandia", "Fiyi", "Francia", "Gabón", "Gambia", "Georgia", "Ghana", "Granada",
        "Grecia", "Guatemala", "Guinea", "Guinea Ecuatorial", "Guinea-Bisáu", "Guyana",
        "Haití", "Honduras", "Hungría", "India", "Indonesia", "Irak", "Irán", "Irlanda",
        "Islandia", "Israel", "Italia", "Jamaica", "Japón", "Jordania", "Kazajistán",
        "Kenia", "Kirguistán", "Kiribati", "Kuwait", "Laos", "Lesoto", "Letonia", "Líbano",
        "Liberia", "Libia", "Liechtenstein", "Lituania", "Luxemburgo", "Madagascar",
        "Malasia", "Malawi", "Maldivas", "Malí", "Malta", "Marruecos", "Mauricio",
        "Mauritania", "México", "Micronesia", "Moldavia", "Mónaco", "Mongolia",
        "Montenegro", "Mozambique", "Namibia", "Nauru", "Nepal", "Nicaragua", "Níger",
        "Nigeria", "Noruega", "Nueva Zelanda", "Omán", "Países Bajos", "Pakistán", "Palaos",
        "Panamá", "Papúa Nueva Guinea", "Paraguay", "Perú", "Polonia", "Portugal",
        "Palestina", "Qatar", "Reino Unido", "República Centroafricana", "República Checa",
        "República del Congo", "República Democrática del Congo", "República Dominicana",
        "Ruanda", "Rumanía", "Rusia", "Samoa", "San Cristóbal y Nieves", "San Marino",
        "San Vicente y las Granadinas", "Santa Lucía", "Santo Tomé y Príncipe", "Senegal",
        "Serbia", "Seychelles", "Sierra Leona", "Singapur", "Siria", "Somalia",
        "Sri Lanka", "Sudáfrica", "Sudán", "Sudán del Sur", "Suecia", "Suiza", "Surinam",
        "Tailandia", "Tanzania", "Tayikistán", "Timor Oriental", "Togo", "Tonga",
        "Trinidad y Tobago", "Túnez", "Turkmenistán", "Turquía", "Tuvalu", "Ucrania",
        "Uganda", "Uruguay", "Uzbekistán", "Vanuatu", "Vaticano", "Venezuela", "Vietnam",
        "Yibuti", "Yemen", "Zambia", "Zimbabue"
    ];

    /// <summary>Alias frecuentes → nombre canónico que se guarda en la BD
    /// ("Holanda" se guarda como "Países Bajos").</summary>
    private static readonly IReadOnlyDictionary<string, string> AliasCanonicos =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["EE.UU."] = "Estados Unidos",
            ["EEUU"] = "Estados Unidos",
            ["USA"] = "Estados Unidos",
            ["Estados Unidos de América"] = "Estados Unidos",
            ["UK"] = "Reino Unido",
            ["Paises Bajos"] = "Países Bajos",
            ["Holanda"] = "Países Bajos",
            ["Chequia"] = "República Checa",
            ["Rusia (Federación Rusa)"] = "Rusia",
            ["Taiwán"] = "Taiwán",
            ["Viet Nam"] = "Vietnam"
        };

    /// <summary>SearchFunc de MudAutocomplete: filtra por "contiene" sin importar
    /// mayúsculas ni acentos; con texto vacío ofrece los más consultados.</summary>
    public static Task<IEnumerable<string>> Buscar(string? texto)
    {
        IEnumerable<string> sugerencias = Todos.Concat(AliasCanonicos.Keys);

        if (!string.IsNullOrWhiteSpace(texto))
        {
            var filtro = SinAcentos(texto.Trim());
            sugerencias = sugerencias.Where(p => SinAcentos(p).Contains(filtro, StringComparison.OrdinalIgnoreCase));
        }

        return Task.FromResult(sugerencias
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.CurrentCultureIgnoreCase)
            .Take(15));
    }

    /// <summary>¿El texto es EXACTAMENTE un país de la lista o un alias válido?
    /// (El campo País es lista cerrada: lo que no figure acá no se guarda.)</summary>
    public static bool Existe(string? nombre)
        => Canonicalizar(nombre) != null;

    /// <summary>Código de marcación internacional (E.164) del país canónico,
    /// p. ej. "Uruguay" → "+598", "Burundi" → "+257". Null si no se conoce.
    /// Alimenta el texto de ayuda del campo Teléfono, para que muestre el
    /// prefijo del país elegido y no siempre el uruguayo.</summary>
    public static string? CodigoMarcacion(string? paisCanonico)
    {
        if (string.IsNullOrWhiteSpace(paisCanonico))
            return null;

        return _codigosMarcacion.TryGetValue(paisCanonico.Trim(), out var codigo)
            ? codigo
            : null;
    }

    /// <summary>Códigos de país E.164 por nombre canónico (mismos nombres que la lista).</summary>
    private static readonly IReadOnlyDictionary<string, string> _codigosMarcacion =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Afganistán"] = "+93", ["Albania"] = "+355", ["Alemania"] = "+49",
            ["Andorra"] = "+376", ["Angola"] = "+244", ["Antigua y Barbuda"] = "+1268",
            ["Arabia Saudita"] = "+966", ["Argelia"] = "+213", ["Argentina"] = "+54",
            ["Armenia"] = "+374", ["Australia"] = "+61", ["Austria"] = "+43",
            ["Azerbaiyán"] = "+994", ["Bahamas"] = "+1242", ["Bangladés"] = "+880",
            ["Barbados"] = "+1246", ["Baréin"] = "+973", ["Bélgica"] = "+32",
            ["Belice"] = "+501", ["Benín"] = "+229", ["Bielorrusia"] = "+375",
            ["Birmania (Myanmar)"] = "+95", ["Bolivia"] = "+591",
            ["Bosnia y Herzegovina"] = "+387", ["Botsuana"] = "+267", ["Brasil"] = "+55",
            ["Brunéi"] = "+673", ["Bulgaria"] = "+359", ["Burkina Faso"] = "+226",
            ["Burundi"] = "+257", ["Bután"] = "+975", ["Cabo Verde"] = "+238",
            ["Camboya"] = "+855", ["Camerún"] = "+237", ["Canadá"] = "+1", ["Chad"] = "+235",
            ["Chile"] = "+56", ["China"] = "+86", ["Chipre"] = "+357", ["Colombia"] = "+57",
            ["Comoras"] = "+269", ["Corea del Norte"] = "+850", ["Corea del Sur"] = "+82",
            ["Costa de Marfil"] = "+225", ["Costa Rica"] = "+506", ["Croacia"] = "+385",
            ["Cuba"] = "+53", ["Dinamarca"] = "+45", ["Dominica"] = "+1767",
            ["Ecuador"] = "+593", ["Egipto"] = "+20", ["El Salvador"] = "+503",
            ["Emiratos Árabes Unidos"] = "+971", ["Eritrea"] = "+291", ["Eslovaquia"] = "+421",
            ["Eslovenia"] = "+386", ["España"] = "+34", ["Estados Unidos"] = "+1",
            ["Estonia"] = "+372", ["Esuatini"] = "+268", ["Etiopía"] = "+251",
            ["Filipinas"] = "+63", ["Finlandia"] = "+358", ["Fiyi"] = "+679",
            ["Francia"] = "+33", ["Gabón"] = "+241", ["Gambia"] = "+220", ["Georgia"] = "+995",
            ["Ghana"] = "+233", ["Granada"] = "+1473", ["Grecia"] = "+30", ["Guatemala"] = "+502",
            ["Guinea"] = "+224", ["Guinea Ecuatorial"] = "+240", ["Guinea-Bisáu"] = "+245",
            ["Guyana"] = "+592", ["Haití"] = "+509", ["Honduras"] = "+504", ["Hungría"] = "+36",
            ["India"] = "+91", ["Indonesia"] = "+62", ["Irak"] = "+964", ["Irán"] = "+98",
            ["Irlanda"] = "+353", ["Islandia"] = "+354", ["Israel"] = "+972", ["Italia"] = "+39",
            ["Jamaica"] = "+1876", ["Japón"] = "+81", ["Jordania"] = "+962",
            ["Kazajistán"] = "+7", ["Kenia"] = "+254", ["Kirguistán"] = "+996",
            ["Kiribati"] = "+686", ["Kuwait"] = "+965", ["Laos"] = "+856", ["Lesoto"] = "+266",
            ["Letonia"] = "+371", ["Líbano"] = "+961", ["Liberia"] = "+231", ["Libia"] = "+218",
            ["Liechtenstein"] = "+423", ["Lituania"] = "+370", ["Luxemburgo"] = "+352",
            ["Madagascar"] = "+261", ["Malasia"] = "+60", ["Malawi"] = "+265",
            ["Maldivas"] = "+960", ["Malí"] = "+223", ["Malta"] = "+356", ["Marruecos"] = "+212",
            ["Mauricio"] = "+230", ["Mauritania"] = "+222", ["México"] = "+52",
            ["Micronesia"] = "+691", ["Moldavia"] = "+373", ["Mónaco"] = "+377",
            ["Mongolia"] = "+976", ["Montenegro"] = "+382", ["Mozambique"] = "+258",
            ["Namibia"] = "+264", ["Nauru"] = "+674", ["Nepal"] = "+977",
            ["Nicaragua"] = "+505", ["Níger"] = "+227", ["Nigeria"] = "+234",
            ["Noruega"] = "+47", ["Nueva Zelanda"] = "+64", ["Omán"] = "+968",
            ["Países Bajos"] = "+31", ["Pakistán"] = "+92", ["Palaos"] = "+680",
            ["Panamá"] = "+507", ["Papúa Nueva Guinea"] = "+675", ["Paraguay"] = "+595",
            ["Perú"] = "+51", ["Polonia"] = "+48", ["Portugal"] = "+351",
            ["Palestina"] = "+970", ["Qatar"] = "+974", ["Reino Unido"] = "+44",
            ["República Centroafricana"] = "+236", ["República Checa"] = "+420",
            ["República del Congo"] = "+242", ["República Democrática del Congo"] = "+243",
            ["República Dominicana"] = "+1809", ["Ruanda"] = "+250", ["Rumanía"] = "+40",
            ["Rusia"] = "+7", ["Samoa"] = "+685", ["San Cristóbal y Nieves"] = "+1869",
            ["San Marino"] = "+378", ["San Vicente y las Granadinas"] = "+1784",
            ["Santa Lucía"] = "+1758", ["Santo Tomé y Príncipe"] = "+239",
            ["Senegal"] = "+221", ["Serbia"] = "+381", ["Seychelles"] = "+248",
            ["Sierra Leona"] = "+232", ["Singapur"] = "+65", ["Siria"] = "+963",
            ["Somalia"] = "+252", ["Sri Lanka"] = "+94", ["Sudáfrica"] = "+27",
            ["Sudán"] = "+249", ["Sudán del Sur"] = "+211", ["Suecia"] = "+46",
            ["Suiza"] = "+41", ["Surinam"] = "+597", ["Tailandia"] = "+66",
            ["Tanzania"] = "+255", ["Tayikistán"] = "+992", ["Timor Oriental"] = "+670",
            ["Togo"] = "+228", ["Tonga"] = "+676", ["Trinidad y Tobago"] = "+1868",
            ["Túnez"] = "+216", ["Turkmenistán"] = "+993", ["Turquía"] = "+90",
            ["Tuvalu"] = "+688", ["Ucrania"] = "+380", ["Uganda"] = "+256",
            ["Uruguay"] = "+598", ["Uzbekistán"] = "+998", ["Vanuatu"] = "+678",
            ["Vaticano"] = "+39", ["Venezuela"] = "+58", ["Vietnam"] = "+84",
            ["Yibuti"] = "+253", ["Yemen"] = "+967", ["Zambia"] = "+260",
            ["Zimbabue"] = "+263"
        };

    /// <summary>Nombre canónico del país (el de la lista; los alias se
    /// traducen). Null si no existe en la lista.</summary>
    public static string? Canonicalizar(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            return null;

        var normalizado = SinAcentos(nombre.Trim());

        var canonico = Todos.FirstOrDefault(p =>
            SinAcentos(p).Equals(normalizado, StringComparison.OrdinalIgnoreCase));
        if (canonico != null)
            return canonico;

        foreach (var (alias, destino) in AliasCanonicos)
        {
            if (SinAcentos(alias).Equals(normalizado, StringComparison.OrdinalIgnoreCase))
                return destino;
        }

        return null;
    }

    /// <summary>Minúsculas sin diacríticos ("Uruguay" ~ "uruguay", "Perú" ~ "peru").</summary>
    private static string SinAcentos(string texto)
    {
        var descompuesto = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(descompuesto.Length);
        foreach (var c in descompuesto)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) !=
                System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }
        return sb.ToString().ToLowerInvariant();
    }
}

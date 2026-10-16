using System.Text;
using System.Text.RegularExpressions;
using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.Contactos;
using GuarinoApp.Application.Modules.Contactos.Commands.ImportarContactos;
using MediatR;

namespace GuarinoApp.Infrastructure.Services.Skills;

/// <summary>
/// Importa contactos desde texto pegado en el chat (CSV o exportación de un
/// chat de WhatsApp / firma de correo). A diferencia de las demás skills no
/// es una consulta: recibe el mensaje del usuario (<see cref="ISkillConEntrada"/>),
/// lo parsea y da de alta los contactos vía ImportarContactosCommand, con las
/// mismas validaciones, deduplicación y auditoría que la pantalla de Contactos.
/// </summary>
public sealed class ContactosImportSkill : ISkillConEntrada
    {
    private const int MaximoPorLote = 300;

    // Quita la línea/encabezado de instrucción: "Importar contactos: ...",
    // "Cargá estos contactos de whatsapp:", etc.
    private static readonly Regex InstruccionInicial = new(
        @"^\s*(import(ar|a|á)|carg(a|á)|agreg(a|á)|peg(a|á)|carga de|alta de|lista de)\s+(est[oa]s?|la|las|los|el|mis|una)?\s*(lista\s+de\s+)?contactos?(?:\s+[^:\n]{0,30})?\s*[:：]?\s*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public ContactosImportSkill(IMediator mediator, ICurrentUserService currentUser)
        {
        _mediator = mediator;
        _currentUser = currentUser;
        }

    public string Name => "importar_contactos";

    public string Description =>
        "Importa contactos nuevos desde texto pegado (CSV o chat de WhatsApp/correo): " +
        "parsea, valida y los da de alta en Contactos. No sirve para consultar la lista de contactos.";

    public IReadOnlyCollection<string> Ejemplos => new[]
        {
        "Importar contactos: "
        };

    public IReadOnlyCollection<string> Keywords => new[]
        {
        "importar contactos", "importar contacto", "importa contactos", "importa contacto",
        "importar estos contactos", "importar los contactos",
        "cargar contactos", "cargar contacto", "carga de contactos",
        "cargar estos contactos", "cargar los contactos",
        "cargá estos contactos", "cargá los contactos", "carga estos contactos", "carga los contactos",
        "agregar contactos", "agregar contacto",
        "agregar estos contactos", "agregar los contactos", "agrega estos contactos", "agrega los contactos",
        "agregá estos contactos", "agregá los contactos",
        "contactos de whatsapp", "contactos del whatsapp",
        "importar de whatsapp", "importar desde whatsapp",
        "alta de contactos", "alta masiva de contactos", "importar lista de contactos"
        };

    public Task<SkillOutput> ExecuteAsync(CancellationToken cancellationToken = default)
        => ExecuteAsync(string.Empty, cancellationToken);

    public async Task<SkillOutput> ExecuteAsync(string entrada, CancellationToken cancellationToken = default)
        {
        // Misma política de permisos que la pantalla /contactos (Admin/Gerencia):
        // el chat además lo usan Contador y Operador.
        if (!_currentUser.IsInRole("Admin") && !_currentUser.IsInRole("Gerencia"))
            return SkillOutput.Error("No tenés permisos para importar contactos (se requiere perfil Admin o Gerencia).");

        var texto = QuitarInstruccion(entrada);
        if (string.IsNullOrWhiteSpace(texto))
            return SkillOutput.Error(Ayuda());

        var (filas, error) = ContactoImportParser.PareceCsv(texto)
            ? ContactoImportParser.ParseCsv(texto)
            : ContactoImportParser.ParseTexto(texto);

        if (error != null)
            return SkillOutput.Error(error + "\n" + Ayuda());
        if (filas.Count == 0)
            return SkillOutput.Error(Ayuda());

        var recorte = filas.Count > MaximoPorLote ? filas.Take(MaximoPorLote).ToList() : filas;
        var resultado = await _mediator.Send(
            new ImportarContactosCommand(recorte, "Asistente IA"), cancellationToken);

        if (!resultado.Success)
            return SkillOutput.Error(resultado.Error ?? "No se pudo importar la lista de contactos.");

        var partes = new List<string> { $"Se dieron de alta {resultado.Creados} contacto(s) nuevos" };
        if (resultado.Duplicados > 0)
            partes.Add($"{resultado.Duplicados} ya existían y se omitieron");
        if (resultado.Invalidos > 0)
            partes.Add($"{resultado.Invalidos} fila(s) inválidas");

        var texto1 = new StringBuilder("Importación de contactos finalizada: " + string.Join("; ", partes) + ".");
        if (recorte.Count < filas.Count)
            texto1.Append($" (El tope por lote es {MaximoPorLote}: se importaron los primeros {recorte.Count} de {filas.Count}.)");
        texto1.Append("\nRevisá la lista en la pantalla de Contactos: el origen de estos contactos queda marcado como «Asistente IA».");
        if (resultado.Detalle.Count > 0)
            texto1.Append("\nDetalle:\n- " + string.Join("\n- ", resultado.Detalle));

        return SkillOutput.Ok(texto1.ToString());
        }

    /// <summary>Quita la línea de instrucción inicial, conservando lo pegado debajo.</summary>
    private static string QuitarInstruccion(string mensaje)
        {
        if (string.IsNullOrWhiteSpace(mensaje))
            return string.Empty;

        var lineas = mensaje.Replace("\r", "").Split('\n');
        if (InstruccionInicial.IsMatch(lineas[0]))
            {
            lineas[0] = InstruccionInicial.Replace(lineas[0], string.Empty);
            return string.Join("\n", lineas).Trim();
            }

        return mensaje.Trim();
        }

    private static string Ayuda()
        => "Para importar contactos escribí «Importar contactos:» y pegá abajo las líneas. Formatos aceptados:\n" +
           "- Chat de WhatsApp: «María López: +598 991 234 567» (nombre antes de los dos puntos)\n" +
           "- Lista simple: «Juan Pérez, juan@empresa.uy, +598 999 888 777»\n" +
           "- CSV con encabezado: «Nombre;Empresa;Correo;Teléfono» con una fila por contacto.\n" +
           "Los contactos que ya existen se omiten solos. También podés importar desde la pantalla de Contactos.";
    }

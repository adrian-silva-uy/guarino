using GuarinoApp.Application.Modules.Contactos.Dtos;
using MediatR;

namespace GuarinoApp.Application.Modules.Contactos.Commands.ImportarContactos;

/// <summary>
/// Alta masiva de contactos desde el importador de la pantalla de Contactos
/// (CSV o texto pegado) o desde el Asistente IA. Omite duplicados por correo,
/// teléfono o nombre+empresa y marca el origen de la importación.
/// </summary>
public record ImportarContactosCommand(List<ContactoImportarDto> Contactos, string Origen)
    : IRequest<ImportarContactosResultDto>;

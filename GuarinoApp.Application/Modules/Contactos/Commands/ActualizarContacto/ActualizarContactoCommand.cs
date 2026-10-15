using GuarinoApp.Application.Modules.Contactos.Dtos;
using MediatR;

namespace GuarinoApp.Application.Modules.Contactos.Commands.ActualizarContacto;

/// <summary>Edición de un contacto comercial existente.</summary>
public record ActualizarContactoCommand(
    int Id,
    string Nombre,
    string Empresa,
    string? Pais,
    string? Email,
    string? Telefono,
    string? Interes,
    string? Notas,
    string Estado) : IRequest<ContactoResultDto>;

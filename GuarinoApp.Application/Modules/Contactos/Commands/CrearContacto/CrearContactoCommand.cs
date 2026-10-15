using GuarinoApp.Application.Modules.Contactos.Dtos;
using MediatR;

namespace GuarinoApp.Application.Modules.Contactos.Commands.CrearContacto;

/// <summary>Alta de un contacto comercial (lead).</summary>
public record CrearContactoCommand(
    string Nombre,
    string Empresa,
    string? Pais,
    string? Email,
    string? Telefono,
    string? Interes,
    string? Notas,
    string Estado) : IRequest<ContactoResultDto>;

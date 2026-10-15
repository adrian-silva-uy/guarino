using GuarinoApp.Application.Modules.Contactos.Dtos;
using MediatR;

namespace GuarinoApp.Application.Modules.Contactos.Commands.EliminarContacto;

/// <summary>Baja (borrado lógico) de un contacto comercial.</summary>
public record EliminarContactoCommand(int Id) : IRequest<ContactoResultDto>;

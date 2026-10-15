using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.Contactos.Dtos;
using GuarinoApp.Domain.Entities;
using MediatR;

namespace GuarinoApp.Application.Modules.Contactos.Commands.CrearContacto;

public class CrearContactoCommandHandler : IRequestHandler<CrearContactoCommand, ContactoResultDto>
    {
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public CrearContactoCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser, IAuditService audit)
        {
        _context = context;
        _currentUser = currentUser;
        _audit = audit;
        }

    public async Task<ContactoResultDto> Handle(CrearContactoCommand request, CancellationToken cancellationToken)
        {
        var error = ContactoValidacion.Validar(
            request.Nombre, request.Empresa, request.Pais, request.Email,
            request.Telefono, request.Interes, request.Notas, request.Estado);
        if (error != null)
            return ContactoResultDto.Fail(error);

        var contacto = new Contacto
            {
            Nombre = request.Nombre.Trim(),
            Empresa = request.Empresa.Trim(),
            Pais = string.IsNullOrWhiteSpace(request.Pais) ? null : request.Pais.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            Telefono = string.IsNullOrWhiteSpace(request.Telefono) ? null : request.Telefono.Trim(),
            Interes = string.IsNullOrWhiteSpace(request.Interes) ? null : request.Interes.Trim(),
            Notas = string.IsNullOrWhiteSpace(request.Notas) ? null : request.Notas.Trim(),
            Estado = request.Estado,
            CreatedBy = _currentUser.UserName
            };

        _context.Contactos.Add(contacto);
        await _context.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(
            "Create",
            "Contactos",
            $"Alta de contacto {contacto.Nombre} ({contacto.Empresa}) por {_currentUser.UserName}.");

        return ContactoResultDto.Ok();
        }
    }

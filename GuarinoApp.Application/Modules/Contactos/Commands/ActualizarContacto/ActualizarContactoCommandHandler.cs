using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.Contactos.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GuarinoApp.Application.Modules.Contactos.Commands.ActualizarContacto;

public class ActualizarContactoCommandHandler : IRequestHandler<ActualizarContactoCommand, ContactoResultDto>
    {
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public ActualizarContactoCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser, IAuditService audit)
        {
        _context = context;
        _currentUser = currentUser;
        _audit = audit;
        }

    public async Task<ContactoResultDto> Handle(ActualizarContactoCommand request, CancellationToken cancellationToken)
        {
        var contacto = await _context.Contactos
            .FirstOrDefaultAsync(c => c.Id == request.Id && !c.IsDeleted, cancellationToken);
        if (contacto == null)
            return ContactoResultDto.Fail("El contacto ya no existe.");

        var error = ContactoValidacion.Validar(
            request.Nombre, request.Empresa, request.Pais, request.Email,
            request.Telefono, request.Interes, request.Notas, request.Estado);
        if (error != null)
            return ContactoResultDto.Fail(error);

        var nombreAnterior = contacto.Nombre;

        contacto.Nombre = request.Nombre.Trim();
        contacto.Empresa = request.Empresa.Trim();
        contacto.Pais = string.IsNullOrWhiteSpace(request.Pais) ? null : request.Pais.Trim();
        contacto.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        contacto.Telefono = string.IsNullOrWhiteSpace(request.Telefono) ? null : request.Telefono.Trim();
        contacto.Interes = string.IsNullOrWhiteSpace(request.Interes) ? null : request.Interes.Trim();
        contacto.Notas = string.IsNullOrWhiteSpace(request.Notas) ? null : request.Notas.Trim();
        contacto.Estado = request.Estado;
        contacto.UpdatedAt = DateTime.UtcNow;
        contacto.UpdatedBy = _currentUser.UserName;

        try
            {
            await _context.SaveChangesAsync(cancellationToken);
            }
        catch (DbUpdateConcurrencyException)
            {
            // Otra sesión lo editó a la vez (mismo criterio que UpdateUser).
            return ContactoResultDto.Fail("El contacto fue modificado por otra sesión. Actualizá y volvé a intentar.");
            }

        await _audit.LogAsync(
            "Update",
            "Contactos",
            $"Edición de contacto {contacto.Nombre} ({contacto.Empresa}) por {_currentUser.UserName}." +
            (nombreAnterior != contacto.Nombre ? $" Antes: {nombreAnterior}." : string.Empty));

        return ContactoResultDto.Ok();
        }
    }

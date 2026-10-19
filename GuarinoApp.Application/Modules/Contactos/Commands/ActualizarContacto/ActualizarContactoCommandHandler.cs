using GuarinoApp.Application.Common;
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

        var nombreAnterior = contacto.Nombre;

        // Misma receta que CrearContacto: sanitiza primero, después valida.
        var nombre = SanitizadorTexto.UnaLinea(request.Nombre, 500);
        var empresa = SanitizadorTexto.UnaLinea(request.Empresa, 500);
        var pais = SanitizadorTexto.UnaLinea(request.Pais, 300);
        var email = SanitizadorTexto.UnaLinea(request.Email, 300);
        var telefono = SanitizadorTexto.UnaLinea(request.Telefono, 300);
        var interes = SanitizadorTexto.UnaLinea(request.Interes, 500);
        var notas = SanitizadorTexto.Multilinea(request.Notas, 4000);

        var error = ContactoValidacion.Validar(
            nombre, empresa, pais, email,
            telefono, interes, notas, request.Estado);
        if (error != null)
            return ContactoResultDto.Fail(error);

        // E.164: forma canónica al guardar (misma regla que el alta).
        if (!string.IsNullOrWhiteSpace(telefono))
            telefono = TelefonoE164.Normalizar(telefono);

        contacto.Nombre = nombre;
        contacto.Empresa = empresa;
        contacto.Pais = string.IsNullOrWhiteSpace(pais) ? null : pais;
        contacto.Email = string.IsNullOrWhiteSpace(email) ? null : email;
        contacto.Telefono = string.IsNullOrWhiteSpace(telefono) ? null : telefono;
        contacto.Interes = string.IsNullOrWhiteSpace(interes) ? null : interes;
        contacto.Notas = string.IsNullOrWhiteSpace(notas) ? null : notas;
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

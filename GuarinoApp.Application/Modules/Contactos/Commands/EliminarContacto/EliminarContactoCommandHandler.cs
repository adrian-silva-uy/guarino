using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.Contactos.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GuarinoApp.Application.Modules.Contactos.Commands.EliminarContacto;

public class EliminarContactoCommandHandler : IRequestHandler<EliminarContactoCommand, ContactoResultDto>
    {
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public EliminarContactoCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser, IAuditService audit)
        {
        _context = context;
        _currentUser = currentUser;
        _audit = audit;
        }

    public async Task<ContactoResultDto> Handle(EliminarContactoCommand request, CancellationToken cancellationToken)
        {
        var contacto = await _context.Contactos
            .FirstOrDefaultAsync(c => c.Id == request.Id && !c.IsDeleted, cancellationToken);
        if (contacto == null)
            return ContactoResultDto.Fail("El contacto ya no existe.");

        // Borrado lógico: el historial de leads no se pierde.
        contacto.IsDeleted = true;
        contacto.UpdatedAt = DateTime.UtcNow;
        contacto.UpdatedBy = _currentUser.UserName;
        await _context.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(
            "Delete",
            "Contactos",
            $"Baja de contacto {contacto.Nombre} ({contacto.Empresa}) por {_currentUser.UserName}.");

        return ContactoResultDto.Ok();
        }
    }

using GuarinoApp.Application.Common;
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
        // Sanitiza ANTES de validar: caracteres de control, saltos no
        // deseados y pseudoetiquetas HTML jamás llegan a la BD (y las
        // longitudes de ContactoValidacion se miden sobre texto limpio).
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

        // E.164 (norma internacional): la entrada es flexible, lo que se
        // guarda es la forma canónica "+<hasta 15 dígitos>".
        if (!string.IsNullOrWhiteSpace(telefono))
            telefono = TelefonoE164.Normalizar(telefono);

        var contacto = new Contacto
            {
            Nombre = nombre,
            Empresa = empresa,
            Pais = string.IsNullOrWhiteSpace(pais) ? null : pais,
            Email = string.IsNullOrWhiteSpace(email) ? null : email,
            Telefono = string.IsNullOrWhiteSpace(telefono) ? null : telefono,
            Interes = string.IsNullOrWhiteSpace(interes) ? null : interes,
            Notas = string.IsNullOrWhiteSpace(notas) ? null : notas,
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

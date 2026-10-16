using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.Contactos.Dtos;
using GuarinoApp.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GuarinoApp.Application.Modules.Contactos.Commands.ImportarContactos;

/// <summary>
/// Importación masiva de contactos: valida cada fila con las mismas reglas que
/// el alta manual, omite duplicados (contra la BD y dentro del propio lote,
/// por correo, teléfono o nombre+empresa) y deja todo en una sola auditoría.
/// </summary>
public class ImportarContactosCommandHandler : IRequestHandler<ImportarContactosCommand, ImportarContactosResultDto>
    {
    private const int MaximoPorLote = 300;
    private const int MaximoDetalle = 20;

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public ImportarContactosCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser, IAuditService audit)
        {
        _context = context;
        _currentUser = currentUser;
        _audit = audit;
        }

    public async Task<ImportarContactosResultDto> Handle(ImportarContactosCommand request, CancellationToken cancellationToken)
        {
        if (request.Contactos == null || request.Contactos.Count == 0)
            return ImportarContactosResultDto.Fail("No hay contactos para importar.");
        if (request.Contactos.Count > MaximoPorLote)
            return ImportarContactosResultDto.Fail($"La importación admite hasta {MaximoPorLote} contactos por vez.");

        // La BD entera en una sola consulta: con esos mismos sets también se
        // deduplica dentro del lote (sin volver a consultar por fila).
        var existentes = await _context.Contactos
            .Where(c => !c.IsDeleted)
            .Select(c => new { c.Nombre, c.Empresa, c.Email, c.Telefono })
            .ToListAsync(cancellationToken);

        var emails = new HashSet<string>(existentes.Where(c => c.Email != null).Select(c => ClaveEmail(c.Email!)));
        var telefonos = new HashSet<string>(existentes.Where(c => c.Telefono != null).Select(c => ClaveTelefono(c.Telefono!)));
        var nombresEmpresas = new HashSet<string>(existentes.Select(c => ClaveNombreEmpresa(c.Nombre, c.Empresa)));

        var resultado = ImportarContactosResultDto.Ok();
        var creados = 0;

        foreach (var fila in request.Contactos)
            {
            var error = ContactoValidacion.Validar(
                fila.Nombre, fila.Empresa, fila.Pais, fila.Email,
                fila.Telefono, fila.Interes, fila.Notas, ContactoEstados.Nuevo);
            if (error != null)
                {
                resultado.Invalidos++;
                AgregarDetalle(resultado, $"«{NombreCorto(fila.Nombre)}»: {error}");
                continue;
                }

            var email = string.IsNullOrWhiteSpace(fila.Email) ? null : fila.Email.Trim();
            var telefono = string.IsNullOrWhiteSpace(fila.Telefono) ? null : fila.Telefono.Trim();

            var esDuplicado =
                (email != null && emails.Contains(ClaveEmail(email))) ||
                (telefono != null && telefonos.Contains(ClaveTelefono(telefono))) ||
                nombresEmpresas.Contains(ClaveNombreEmpresa(fila.Nombre, fila.Empresa));

            if (esDuplicado)
                {
                resultado.Duplicados++;
                AgregarDetalle(resultado, $"«{NombreCorto(fila.Nombre)}»: ya existe en Contactos.");
                continue;
                }

            if (email != null) emails.Add(ClaveEmail(email));
            if (telefono != null) telefonos.Add(ClaveTelefono(telefono));
            nombresEmpresas.Add(ClaveNombreEmpresa(fila.Nombre, fila.Empresa));

            _context.Contactos.Add(new Contacto
                {
                Nombre = fila.Nombre.Trim(),
                Empresa = fila.Empresa.Trim(),
                Pais = string.IsNullOrWhiteSpace(fila.Pais) ? null : fila.Pais.Trim(),
                Email = email,
                Telefono = telefono,
                Interes = string.IsNullOrWhiteSpace(fila.Interes) ? null : fila.Interes.Trim(),
                Notas = string.IsNullOrWhiteSpace(fila.Notas) ? null : fila.Notas.Trim(),
                Estado = ContactoEstados.Nuevo,
                Origen = string.IsNullOrWhiteSpace(request.Origen) ? null : request.Origen.Trim(),
                CreatedBy = _currentUser.UserName
                });
            creados++;
            }

        if (creados > 0)
            {
            await _context.SaveChangesAsync(cancellationToken);
            await _audit.LogAsync(
                "Importar",
                "Contactos",
                $"Importación masiva de {creados} contacto(s) desde {request.Origen} por {_currentUser.UserName} " +
                $"({resultado.Duplicados} duplicados, {resultado.Invalidos} inválidos).");
            }

        resultado.Creados = creados;
        return resultado;
        }

    private static void AgregarDetalle(ImportarContactosResultDto resultado, string motivo)
        {
        if (resultado.Detalle.Count < MaximoDetalle)
            resultado.Detalle.Add(motivo);
        }

    /// <summary>Clave de deduplicación por correo.</summary>
    private static string ClaveEmail(string email)
        => email.Trim().ToLowerInvariant();

    /// <summary>Clave de deduplicación por teléfono: solo dígitos (+598 991 12 345 = 59899112345).</summary>
    private static string ClaveTelefono(string telefono)
        {
        var digitos = new string(telefono.Where(char.IsDigit).ToArray());
        return digitos.Length >= 7 ? digitos : telefono.Trim().ToLowerInvariant();
        }

    /// <summary>Clave de deduplicación por nombre+empresa (sin acentos ni dobles espacios).</summary>
    private static string ClaveNombreEmpresa(string nombre, string empresa)
        => Normalizar(nombre) + "|" + Normalizar(empresa);

    private static string Normalizar(string valor)
        {
        var compuesto = (valor ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(compuesto.Length);
        foreach (var ch in compuesto)
            {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                builder.Append(ch);
            }
        return Regex.Replace(builder.ToString().Normalize(NormalizationForm.FormC), @"\s+", " ");
        }

    /// <summary>Nombre corto y seguro para el detalle de errores.</summary>
    private static string NombreCorto(string? nombre)
        {
        var n = (nombre ?? string.Empty).Trim();
        return n.Length <= 40 ? n : n[..40] + "…";
        }
    }

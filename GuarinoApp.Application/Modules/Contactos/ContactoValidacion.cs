using System.Net.Mail;
using GuarinoApp.Domain.Entities;

namespace GuarinoApp.Application.Modules.Contactos;

/// <summary>
/// Validación de contactos. La app valida dentro de los handlers y devuelve
/// Fail(mensaje) (no hay pipeline de FluentValidation registrado).
/// </summary>
public static class ContactoValidacion
    {
    public static string? Validar(string? nombre, string? empresa, string? pais, string? email,
        string? telefono, string? interes, string? notas, string? estado)
        {
        if (string.IsNullOrWhiteSpace(nombre))
            return "Ingresá el nombre del contacto.";
        if (nombre.Trim().Length > 150)
            return "El nombre supera los 150 caracteres.";

        if (string.IsNullOrWhiteSpace(empresa))
            return "Ingresá la empresa u organización.";
        if (empresa.Trim().Length > 200)
            return "La empresa supera los 200 caracteres.";

        if (pais != null && pais.Trim().Length > 100)
            return "El país supera los 100 caracteres.";

        if (!string.IsNullOrWhiteSpace(email) && !EsEmailValido(email))
            return "El correo no tiene un formato válido.";

        if (!string.IsNullOrWhiteSpace(telefono) && !ContactoImportParser.EsTelefono(telefono))
            return "El teléfono no tiene un formato válido (8-15 dígitos; con + o 00 de país si se quiere; sin letras ni @).";

        if (telefono != null && telefono.Trim().Length > 50)
            return "El teléfono supera los 50 caracteres.";

        if (interes != null && interes.Trim().Length > 300)
            return "Lo que le interesó supera los 300 caracteres.";

        if (notas != null && notas.Length > 2000)
            return "Las notas superan los 2000 caracteres.";

        if (estado == null || !Array.Exists(ContactoEstados.Todos, e => e == estado))
            return "Estado inválido.";

        return null;
        }

    private static bool EsEmailValido(string email)
        {
        try
            {
            _ = new MailAddress(email.Trim());
            return true;
            }
        catch (FormatException)
            {
            return false;
            }
        }
    }

using System.Collections;
using System.Reflection;
using GuarinoApp.Application.Common.Security;
using MediatR;

namespace GuarinoApp.Application.Common.Behaviors;

/// <summary>
/// Validación y limpieza automática de todos los campos de texto libre de la
/// aplicación, antes de que la petición llegue al handler (OWASP "Input
/// Validation &amp; Sanitization").
///
/// Recorre las propiedades string de toda query/command MediatR (también las
/// de listas y objetos anidados, con profundidad limitada) y les aplica
/// <see cref="InputSanitizer.Sanitize"/>. Así todos los campos de texto
/// quedan cubiertos sin depender de que cada pantalla se acuerde de limpiar.
///
/// Exclusiones: propiedades con nombre que contiene Password/Hash/Token/
/// Secret/Key — modificarlos rompería la verificación del hash PBKDF2 o la
/// integridad de tokens; esos campos se validan por longitud y formato en sus
/// handlers. (Las contraseñas jamás se concatenan en SQL: solo se hashean.)
/// </summary>
public sealed class InputSanitizationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private const int ProfundidadMaxima = 3;

    private static readonly string[] PropiedadesExcluidas =
        { "Password", "Hash", "Token", "Secret", "Key" };

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        Sanitizar(request, 0);
        return await next();
    }

    private static void Sanitizar(object? nodo, int profundidad)
    {
        if (nodo is null || profundidad > ProfundidadMaxima)
            return;

        // Strings y tipos básicos (int, DateTime, bool…) no se introspeccionan.
        if (nodo is string || nodo is ValueType)
            return;

        foreach (var prop in nodo.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanRead)
                continue;

            if (PropiedadesExcluidas.Any(excluida =>
                    prop.Name.Contains(excluida, StringComparison.OrdinalIgnoreCase)))
                continue;

            object? valor;
            try
            {
                valor = prop.GetValue(nodo);
            }
            catch
            {
                // Propiedad con getter que lanza: se ignora, no debe tumbar el proceso.
                continue;
            }

            if (valor is string texto)
            {
                if (prop.CanWrite)
                {
                    var limpio = InputSanitizer.Sanitize(texto);
                    try
                    {
                        prop.SetValue(nodo, limpio);
                    }
                    catch
                    {
                        // Setter no asignable en runtime (init/readonly): se omite.
                    }
                }
                continue;
            }

            if (valor is null)
                continue;

            if (valor is IEnumerable items)
            {
                // Las cadenas ya se cubrieron arriba; los tipos básicos salen abajo.
                foreach (var item in items)
                    Sanitizar(item, profundidad + 1);
                continue;
            }

            Sanitizar(valor, profundidad + 1);
        }
    }
}

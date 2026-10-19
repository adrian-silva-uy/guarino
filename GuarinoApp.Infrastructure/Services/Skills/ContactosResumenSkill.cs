using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.Contactos.Queries.GetContactos;
using MediatR;
using System.Text;

namespace GuarinoApp.Infrastructure.Services.Skills;

/// <summary>
/// Skill de Contactos: resumen real de la carilla de contactos/clientes —
/// total, estados, países, intereses y las altas más recientes. Solo consulta
/// (la importación desde el chat la resuelve ContactosImportSkill).
/// </summary>
public sealed class ContactosResumenSkill : ISkill
{
    private readonly IMediator _mediator;

    public ContactosResumenSkill(IMediator mediator) => _mediator = mediator;

    public string Name => "contactos_clientes";

    public string Description =>
        "Resumen de contactos/clientes/productores cargados: total, estados, países, intereses y altas más recientes.";

    public IReadOnlyCollection<string> Ejemplos => new[]
        {
        "¿Cuántos clientes y productores tenemos cargados?"
        };

    public IReadOnlyCollection<string> Keywords => new[]
    {
        "contacto", "contactos", "cliente", "clientes", "productor", "productores",
        "exportadora", "exportadoras", "comprador", "compradores", "cartera"
    };

    public async Task<SkillOutput> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var contactos = await _mediator.Send(new GetContactosQuery(), cancellationToken);

        if (contactos is not { Count: > 0 })
            return SkillOutput.Ok("No hay contactos cargados todavía en el sistema.");

        var sb = new StringBuilder();
        sb.AppendLine($"Contactos cargados: {contactos.Count} en total.");

        sb.AppendLine("Por estado:");
        foreach (var grupo in contactos.GroupBy(c => string.IsNullOrWhiteSpace(c.Estado) ? "Sin estado" : c.Estado).OrderByDescending(g => g.Count()))
            sb.AppendLine($"- {grupo.Key}: {grupo.Count()}");

        var conPais = contactos.Where(c => !string.IsNullOrWhiteSpace(c.Pais)).ToList();
        if (conPais.Count > 0)
        {
            sb.AppendLine("Por país (top):");
            foreach (var grupo in conPais.GroupBy(c => c.Pais!).OrderByDescending(g => g.Count()).Take(6))
                sb.AppendLine($"- {grupo.Key}: {grupo.Count()}");
        }

        var conInteres = contactos.Where(c => !string.IsNullOrWhiteSpace(c.Interes)).ToList();
        if (conInteres.Count > 0)
        {
            sb.AppendLine("Por interés:");
            foreach (var grupo in conInteres.GroupBy(c => c.Interes!).OrderByDescending(g => g.Count()).Take(6))
                sb.AppendLine($"- {grupo.Key}: {grupo.Count()}");
        }

        sb.AppendLine("Altas más recientes:");
        foreach (var c in contactos.OrderByDescending(c => c.FechaAlta).Take(8))
        {
            var extra = new[] { c.Empresa, c.Pais }.Where(x => !string.IsNullOrWhiteSpace(x));
            sb.AppendLine($"- {c.Nombre} ({string.Join(", ", extra)}) · estado {c.Estado} · alta {c.FechaAlta:yyyy-MM-dd}");
        }

        return SkillOutput.Ok(sb.ToString().TrimEnd());
    }
}

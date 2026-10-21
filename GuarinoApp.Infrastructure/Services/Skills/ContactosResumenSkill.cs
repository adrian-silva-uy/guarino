using GuarinoApp.Application.Common;
using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.Contactos.Queries.GetContactos;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace GuarinoApp.Infrastructure.Services.Skills;

/// <summary>
/// Skill de Contactos: resumen real de la carilla de contactos/clientes —
/// total, estados, países, intereses y las altas más recientes; y de los
/// productores registrados en la tabla Productores. Solo consulta (la
/// importación desde el chat la resuelve ContactosImportSkill).
/// </summary>
public sealed class ContactosResumenSkill : ISkill
{
    private readonly IMediator _mediator;
    private readonly IApplicationDbContext _context;

    public ContactosResumenSkill(IMediator mediator, IApplicationDbContext context)
        => (_mediator, _context) = (mediator, context);

    public string Name => "contactos_clientes";

    public string Description =>
        "Resumen de contactos/clientes cargados (total, estados, países, intereses y altas recientes) y de los productores registrados.";

    public IReadOnlyCollection<string> Ejemplos => new[]
        {
        "¿Cuántos clientes y productores tenemos cargados?"
        };

    public IReadOnlyCollection<string> Keywords => new[]
    {
        "contacto", "contactos", "cliente", "clientes", "productor", "productores",
        "exportadora", "exportadoras", "comprador", "compradores", "cartera"
    };

    // Contactos/clientes: restringido a los roles de administración (matriz de módulos).
    public IReadOnlyCollection<string> RolesPermitidos => RoleNames.Administracion;

    public async Task<SkillOutput> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var contactos = await _mediator.Send(new GetContactosQuery(), cancellationToken);

        // Productores reales de la tabla Productores (los que aportan fruta):
        // antes este dato no se consultaba y la IA respondía "sin métrica".
        var productores = await _context.Productores
            .AsNoTracking()
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.Nombre)
            .Select(p => p.Nombre)
            .ToListAsync(cancellationToken);

        if ((contactos is not { Count: > 0 }) && productores.Count == 0)
            return SkillOutput.Ok("No hay contactos ni productores cargados todavía en el sistema.");

        var sb = new StringBuilder();

        sb.AppendLine(productores.Count > 0
            ? $"Productores registrados: {productores.Count} ({string.Join(", ", productores.Take(10))})."
            : "Productores registrados: ninguno todavía.");

        if (contactos is not { Count: > 0 })
        {
            sb.AppendLine("Contactos/clientes: ninguno cargado todavía.");
            return SkillOutput.Ok(sb.ToString().TrimEnd());
        }

        sb.AppendLine($"Contactos/clientes cargados: {contactos.Count} en total.");

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

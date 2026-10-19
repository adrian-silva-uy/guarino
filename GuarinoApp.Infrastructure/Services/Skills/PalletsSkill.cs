using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace GuarinoApp.Infrastructure.Services.Skills;

/// <summary>
/// Skill de Pallets sin cliente: consulta EF propia (mismo criterio que la
/// pantalla: sin cliente y estado distinto de Bajado) en lugar de la query
/// del módulo, porque esa query devuelve un fallback dummy cuando la tabla
/// está vacía — y los datos dummy jamás deben llegar al modelo.
/// </summary>
public sealed class PalletsSkill : ISkill
{
    private readonly IApplicationDbContext _context;

    public PalletsSkill(IApplicationDbContext context) => _context = context;

    public string Name => "pallets_sin_cliente";

    public string Description =>
        "Pallets sin cliente asignado: cuántos hay, por variedad, estado y contramarca, con los más recientes.";

    public IReadOnlyCollection<string> Ejemplos => new[]
        {
        "¿Cuántos pallets tenemos sin cliente asignado?"
        };

    public IReadOnlyCollection<string> Keywords => new[]
    {
        "pallet", "pallets", "sin cliente", "contramarca", "contramarcas",
        "calibre", "calibres"
    };

    public async Task<SkillOutput> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var pallets = await _context.Pallets
            .AsNoTracking()
            .Where(p => (p.Cliente == null || p.Cliente.Trim().Length == 0)
                        && p.Estado != EstadoPallet.Bajado)
            .Select(p => new { p.Codigo, p.Variedad, Estado = p.Estado.ToString(), p.Contramarca, p.Calibre, Fecha = (DateTime?)p.Fecha })
            .OrderByDescending(p => p.Fecha)
            .Take(200)
            .ToListAsync(cancellationToken);

        if (pallets.Count == 0)
            return SkillOutput.Ok("No hay pallets sin cliente asignado: todos los pallets activos cargados tienen cliente.");

        var sb = new StringBuilder();
        sb.AppendLine($"Pallets sin cliente asignado: {pallets.Count}" + (pallets.Count == 200 ? " (máximo mostrado)" : string.Empty) + ".");

        sb.AppendLine("Por variedad:");
        foreach (var grupo in pallets.GroupBy(p => string.IsNullOrWhiteSpace(p.Variedad) ? "Sin variedad" : p.Variedad).OrderByDescending(g => g.Count()))
            sb.AppendLine($"- {grupo.Key}: {grupo.Count()}");

        sb.AppendLine("Por estado:");
        foreach (var grupo in pallets.GroupBy(p => string.IsNullOrWhiteSpace(p.Estado) ? "Sin estado" : p.Estado).OrderByDescending(g => g.Count()))
            sb.AppendLine($"- {grupo.Key}: {grupo.Count()}");

        var conContramarca = pallets.Where(p => !string.IsNullOrWhiteSpace(p.Contramarca)).ToList();
        if (conContramarca.Count > 0)
        {
            sb.AppendLine("Por contramarca:");
            foreach (var grupo in conContramarca.GroupBy(p => p.Contramarca!).OrderByDescending(g => g.Count()).Take(6))
                sb.AppendLine($"- {grupo.Key}: {grupo.Count()}");
        }

        sb.AppendLine("Más recientes:");
        foreach (var p in pallets.Take(8))
        {
            sb.AppendLine($"- {p.Codigo} · {p.Variedad} · estado {p.Estado} · calibre {p.Calibre} · {p.Fecha:yyyy-MM-dd}");
        }

        return SkillOutput.Ok(sb.ToString().TrimEnd());
    }
}

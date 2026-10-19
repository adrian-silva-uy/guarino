using GuarinoApp.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace GuarinoApp.Infrastructure.Services.Skills;

/// <summary>
/// Skill de Clima/Riesgo: eventos climáticos registrados en los últimos
/// 90 días (heladas, granizo, lluvias, viento, calor) con severidad y
/// kilos perdidos estimados. Consulta EF directa: no hay handler con dummy.
/// </summary>
public sealed class ClimaRiesgoSkill : ISkill
{
    private readonly IApplicationDbContext _context;

    public ClimaRiesgoSkill(IApplicationDbContext context) => _context = context;

    public string Name => "clima_riesgo";

    public string Description =>
        "Eventos climáticos de los últimos 90 días (heladas, granizo, lluvias, viento, calor): cantidad, severidad y kilos perdidos estimados.";

    public IReadOnlyCollection<string> Ejemplos => new[]
        {
        "¿Hubo eventos climáticos recientes (heladas, granizo)? ¿Cuál es el riesgo?"
        };

    public IReadOnlyCollection<string> Keywords => new[]
    {
        "clima", "climatico", "climaticos", "climatica", "climaticas",
        "helada", "heladas", "granizo", "lluvia", "lluvias",
        "viento", "calor", "temperatura", "riesgo climatico", "tiempo atmosferico"
    };

    public async Task<SkillOutput> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var desde = DateTime.Today.AddDays(-90);

        var ventana = _context.EventosClimaticos
            .AsNoTracking()
            .Where(e => !e.IsDeleted && e.Fecha >= desde);

        // Agregados REALES sobre toda la ventana: el detalle va limitado a 25
        // filas, pero los totales nunca pueden reflejar ese cap (con 8.4k
        // eventos automáticos en 90 días decir "25 registrado(s)" sería falso).
        var total = await ventana.CountAsync(cancellationToken);
        if (total == 0)
            return SkillOutput.Ok($"No se registraron eventos climáticos entre el {desde:yyyy-MM-dd} y hoy: sin heladas, granizo ni lluvias excesivas reportadas.");

        var totalKilosPerdidos = await ventana.SumAsync(e => e.KilosPerdidosEstimados ?? 0m, cancellationToken);
        var porSeveridad = await ventana
            .GroupBy(e => string.IsNullOrWhiteSpace(e.SeveridadEstimada) ? "Sin severidad" : e.SeveridadEstimada)
            .Select(g => new { Severidad = g.Key, Cantidad = g.Count() })
            .ToListAsync(cancellationToken);
        var porTipo = await ventana
            .GroupBy(e => string.IsNullOrWhiteSpace(e.TipoEvento) ? "Sin tipo" : e.TipoEvento)
            .Select(g => new { Tipo = g.Key, Cantidad = g.Count() })
            .ToListAsync(cancellationToken);

        var eventos = await ventana
            .Select(e => new
            {
                e.Fecha,
                e.TipoEvento,
                e.SeveridadEstimada,
                e.KilosPerdidosEstimados,
                e.Observaciones
            })
            .OrderByDescending(e => e.Fecha)
            .Take(25)
            .ToListAsync(cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine($"Eventos climáticos desde el {desde:yyyy-MM-dd}: {total.ToString("N0", CultureInfo.InvariantCulture)} en total, " +
                      $"{totalKilosPerdidos.ToString("N0", CultureInfo.InvariantCulture)} kg de pérdida estimada.");

        sb.AppendLine("Por severidad:");
        foreach (var s in porSeveridad.OrderByDescending(s => s.Cantidad))
            sb.AppendLine($"- {s.Severidad}: {s.Cantidad.ToString("N0", CultureInfo.InvariantCulture)}");

        sb.AppendLine("Por tipo de evento:");
        foreach (var t in porTipo.OrderByDescending(t => t.Cantidad))
            sb.AppendLine($"- {t.Tipo}: {t.Cantidad.ToString("N0", CultureInfo.InvariantCulture)}");

        sb.AppendLine("Detalle de los 25 más recientes:");
        foreach (var e in eventos)
        {
            var obs = string.IsNullOrWhiteSpace(e.Observaciones) ? string.Empty : $" · {e.Observaciones}";
            var perdida = e.KilosPerdidosEstimados.HasValue
                ? $" · pérdida estimada {e.KilosPerdidosEstimados.Value.ToString("N0", CultureInfo.InvariantCulture)} kg"
                : string.Empty;
            sb.AppendLine($"- {e.Fecha:yyyy-MM-dd} · {e.TipoEvento} · severidad {e.SeveridadEstimada}{perdida}{obs}");
        }

        return SkillOutput.Ok(sb.ToString().TrimEnd());
    }
}

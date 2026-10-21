using System.ComponentModel;
using GuarinoApp.Application.Common;
using GuarinoApp.Application.Common.Interfaces;
using ModelContextProtocol.Server;

namespace GuarinoApp.Web.Mcp;

/// <summary>
/// Tools del servidor MCP de Guarino: exponen las MISMAS skills del Asistente
/// (datos reales de la base de datos, cero datos inventados) para que un agente
/// externo pueda consultarlas vía el endpoint /mcp.
///
/// Seguridad en profundidad:
/// - El endpoint exige el token de sesión de la app (McpAuthMiddleware), que
///   trae el rol real del usuario.
/// - Cada tool revalida la matriz de módulos contra RolesPermitidos de la
///   skill: fuera de tu perfil devuelve ACCESO RESTRINGIDO sin ejecutar nada.
/// - Toda llamada (permitida o denegada) queda en Auditoría del Sistema.
///
/// Solo lectura: la importación de contactos (escritura) queda fuera de MCP.
/// </summary>
[McpServerToolType]
public sealed class GuarinoMcpTools
{
    /// <summary>Identificadores de skill (contrato: coinciden con ISkill.Name).</summary>
    private static class Skills
    {
        public const string StockPlanta = "stock_planta";
        public const string PesadasRecientes = "pesadas_recientes";
        public const string VolcadosLotes = "volcados_lotes";
        public const string PalletsSinCliente = "pallets_sin_cliente";
        public const string ClimaRiesgo = "clima_riesgo";
        public const string DatosFinancieros = "datos_financieros";
        public const string ProyeccionZafra = "proyeccion_zafra";
        public const string ResumenLiquidaciones = "resumen_liquidaciones";
        public const string ActividadUsuarios = "actividad_usuarios";
        public const string ContactosClientes = "contactos_clientes";
        public const string IndicadoresGerencia = "indicadores_gerencia";
    }

    /// <summary>Skills expuestas como tools de solo lectura (contrato MCP):
    /// la importación de contactos (escritura) NO entra.</summary>
    private static readonly HashSet<string> HerramientasMcp = new()
    {
        Skills.StockPlanta, Skills.PesadasRecientes, Skills.VolcadosLotes,
        Skills.PalletsSinCliente, Skills.ClimaRiesgo, Skills.DatosFinancieros,
        Skills.ProyeccionZafra, Skills.ResumenLiquidaciones,
        Skills.ActividadUsuarios, Skills.ContactosClientes, Skills.IndicadoresGerencia
    };

    private readonly ISkillRegistry _skillRegistry;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;
    private readonly ILogger<GuarinoMcpTools> _logger;

    public GuarinoMcpTools(
        ISkillRegistry skillRegistry,
        ICurrentUserService currentUser,
        IAuditService audit,
        ILogger<GuarinoMcpTools> logger)
        => (_skillRegistry, _currentUser, _audit, _logger) =
            (skillRegistry, currentUser, audit, logger);

    // ── Operación diaria (todos los roles) ──────────────────────────────────

    [McpServerTool(Name = Skills.StockPlanta)]
    [Description("Stock actual en planta por variedad: bins por variedad, total y variedad con más cantidad.")]
    public Task<string> StockPlanta(CancellationToken cancellationToken)
        => EjecutarSkillAsync(Skills.StockPlanta, cancellationToken);

    [McpServerTool(Name = Skills.PesadasRecientes)]
    [Description("Pesadas de los últimos 30 días: cantidad, kilos y bins totales, reparto por báscula y últimas pesadas con lote y productor.")]
    public Task<string> PesadasRecientes(CancellationToken cancellationToken)
        => EjecutarSkillAsync(Skills.PesadasRecientes, cancellationToken);

    [McpServerTool(Name = Skills.VolcadosLotes)]
    [Description("Volcados de fruta de los últimos 60 días: kilos y bins volcados, reparto por variedad y lotes recientes con productor.")]
    public Task<string> VolcadosLotes(CancellationToken cancellationToken)
        => EjecutarSkillAsync(Skills.VolcadosLotes, cancellationToken);

    [McpServerTool(Name = Skills.PalletsSinCliente)]
    [Description("Pallets sin cliente asignado: cuántos hay, por variedad, estado y contramarca, con los más recientes.")]
    public Task<string> PalletsSinCliente(CancellationToken cancellationToken)
        => EjecutarSkillAsync(Skills.PalletsSinCliente, cancellationToken);

    [McpServerTool(Name = Skills.ClimaRiesgo)]
    [Description("Eventos climáticos de los últimos 90 días (heladas, granizo, lluvias, viento, calor): cantidad, severidad y kilos perdidos estimados.")]
    public Task<string> ClimaRiesgo(CancellationToken cancellationToken)
        => EjecutarSkillAsync(Skills.ClimaRiesgo, cancellationToken);

    // ── Contaduría y proyección (Admin/Gerencia/Contador) ────────────────────

    [McpServerTool(Name = Skills.DatosFinancieros)]
    [Description("Datos financieros vigentes: costo por kg, flujo de caja, margen bruto, costo de flete y capacidad instalada. Reservado a Admin, Gerencia y Contador.")]
    public Task<string> DatosFinancieros(CancellationToken cancellationToken)
        => EjecutarSkillAsync(Skills.DatosFinancieros, cancellationToken);

    [McpServerTool(Name = Skills.ProyeccionZafra)]
    [Description("Detalle de la proyección de la próxima zafra: kilos, variación vs. año anterior, bins, camiones y horas de empaque estimadas. Reservado a Admin, Gerencia y Contador.")]
    public Task<string> ProyeccionZafra(CancellationToken cancellationToken)
        => EjecutarSkillAsync(Skills.ProyeccionZafra, cancellationToken);

    [McpServerTool(Name = Skills.ResumenLiquidaciones)]
    [Description("Resumen de liquidaciones: total a pagar, kilos y bins por productor y variedad. Reservado a Admin, Gerencia y Contador.")]
    public Task<string> ResumenLiquidaciones(CancellationToken cancellationToken)
        => EjecutarSkillAsync(Skills.ResumenLiquidaciones, cancellationToken);

    // ── Administración (Admin/Gerencia) ─────────────────────────────────────

    [McpServerTool(Name = Skills.ActividadUsuarios)]
    [Description("Últimos registros de actividad del sistema: usuario, acción, módulo y fecha/hora. Reservado a Admin y Gerencia.")]
    public Task<string> ActividadUsuarios(CancellationToken cancellationToken)
        => EjecutarSkillAsync(Skills.ActividadUsuarios, cancellationToken);

    [McpServerTool(Name = Skills.ContactosClientes)]
    [Description("Resumen de contactos/clientes cargados (total, estados, países, intereses y altas recientes) y de los productores registrados. Reservado a Admin y Gerencia.")]
    public Task<string> ContactosClientes(CancellationToken cancellationToken)
        => EjecutarSkillAsync(Skills.ContactosClientes, cancellationToken);

    [McpServerTool(Name = Skills.IndicadoresGerencia)]
    [Description("Indicadores del Panel de Gerencia: valores vigentes agrupados por categoría con su fecha de medición. Reservado a Admin y Gerencia.")]
    public Task<string> IndicadoresGerencia(CancellationToken cancellationToken)
        => EjecutarSkillAsync(Skills.IndicadoresGerencia, cancellationToken);

    // ── Catálogo ────────────────────────────────────────────────────────────

    [McpServerTool(Name = "consultas_disponibles")]
    [Description("Lista las consultas (tools) que ESTE usuario puede hacer, según su perfil: operación diaria para todos; contaduría y proyección para Admin/Gerencia/Contador; administración para Admin/Gerencia.")]
    public async Task<string> ConsultasDisponibles(CancellationToken cancellationToken)
    {
        var rol = RolActual();
        var permitidas = _skillRegistry.Skills
            .Where(s => HerramientasMcp.Contains(s.Name))
            .Where(s => s.RolesPermitidos.Count == 0
                || (rol is not null && s.RolesPermitidos.Contains(rol)))
            .Select(s => $"- {s.Name}: {DescripcionCorta(s.Description)}")
            .ToList();

        await AuditarAsync("consultas_disponibles", "Catálogo de consultas listado.", permitida: true, rol, cancellationToken);

        return permitidas.Count == 0
            ? "Tu perfil no tiene consultas habilitadas."
            : $"Consultas disponibles para tu perfil ({rol ?? "sin rol"}):\n" + string.Join("\n", permitidas);
    }

    // ── Implementación ──────────────────────────────────────────────────────

    /// <summary>
    /// Ejecuta la skill homónima sobre datos reales tras revalidar la matriz de
    /// módulos. Fuera de tu perfil no se ejecuta ni se devuelve un byte de datos:
    /// ACCESO RESTRINGIDO + auditoría.
    /// </summary>
    private async Task<string> EjecutarSkillAsync(string nombreSkill, CancellationToken cancellationToken)
    {
        var skill = _skillRegistry.Skills.FirstOrDefault(s => s.Name == nombreSkill);
        if (skill is null)
            return $"Error interno: la herramienta '{nombreSkill}' no está registrada.";

        var rol = RolActual();
        if (skill.RolesPermitidos.Count > 0
            && (rol is null || !skill.RolesPermitidos.Contains(rol)))
        {
            await AuditarAsync(nombreSkill,
                $"Consulta MCP restringida por rol: la herramienta '{nombreSkill}' está reservada para {string.Join(", ", skill.RolesPermitidos)}.",
                permitida: false, rol, cancellationToken);
            _logger.LogInformation(
                "MCP: herramienta '{Skill}' denegada al rol {Rol}.", nombreSkill, rol ?? "sin rol");
            return $"ACCESO RESTRINGIDO: la herramienta '{nombreSkill}' está reservada para los roles {string.Join(", ", skill.RolesPermitidos)}. Tu rol: {rol ?? "sin rol"}. El intento quedó registrado en Auditoría del Sistema.";
        }

        var output = await skill.ExecuteAsync(cancellationToken);
        await AuditarAsync(nombreSkill,
            $"Consulta MCP ejecutada (datos reales, éxito: {(output.Success ? "sí" : "no")}).",
            permitida: true, rol, cancellationToken);

        return output.Text;
    }

    /// <summary>Primer rol real del usuario contra la lista canónica (fuente única: RoleNames).</summary>
    private string? RolActual()
        => RoleNames.Todos.FirstOrDefault(r => _currentUser.IsInRole(r));

    /// <summary>Registro de auditoría sin tumbar la llamada: «McpCall» en el módulo MCP.</summary>
    private async Task AuditarAsync(string herramienta, string detalle, bool permitida, string? rol, CancellationToken cancellationToken)
    {
        try
        {
            await _audit.LogAsync(
                action: "McpCall",
                module: "MCP",
                description:
                    $"{(permitida ? "Herramienta" : "Intento de herramienta")} MCP '{herramienta}' " +
                    $"por '{_currentUser.UserName ?? "desconocido"}' ({rol ?? "sin rol"}): {detalle}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MCP: no se pudo auditar la llamada a '{Skill}'.", herramienta);
        }
    }

    /// <summary>Primer segmento de la descripción de la skill (antes del primer ':').</summary>
    private static string DescripcionCorta(string descripcion)
    {
        var corte = descripcion.IndexOf(':');
        return (corte > 0 ? descripcion[..corte] : descripcion).Trim();
    }
}

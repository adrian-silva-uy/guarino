namespace GuarinoApp.Application.Common.Interfaces;

/// <summary>
/// Resultado de la ejecución de una skill.
/// </summary>
/// <param name="Success">true si la consulta trajo datos; false si falló o no hay datos.</param>
/// <param name="Text">Salida en texto lista para inyectarse como contexto del modelo.</param>
public sealed record SkillOutput(bool Success, string Text)
{
    public static SkillOutput Ok(string text) => new(true, text);
    public static SkillOutput Error(string message) => new(false, message);
}

/// <summary>
/// Casación de una skill con el mensaje del usuario, identificando cuál se ejecutó.
/// </summary>
/// <param name="SkillName">Identificador de la skill ejecutada.</param>
/// <param name="Success">true si devolvió datos reales.</param>
/// <param name="Text">Salida en texto de la skill.</param>
/// <param name="Restringido">true si la ejecución se denegó por rol: Text contiene
/// el mensaje de rechazo inteligente (ya auditado) y no se ejecutó la skill.</param>
public sealed record SkillMatch(string SkillName, bool Success, string Text, bool Restringido = false);

/// <summary>
/// Habilidad (skill) invocable por la IA: una consulta real de negocio cuyo
/// resultado se inyecta como contexto en el prompt, de modo que el modelo
/// responda con datos reales del sistema en lugar de inventarlos.
/// </summary>
public interface ISkill
{
    /// <summary>Identificador estable de la skill (ej. "datos_financieros").</summary>
    string Name { get; }

    /// <summary>Qué resuelve la skill, en lenguaje claro.</summary>
    string Description { get; }

    /// <summary>
    /// Ejemplos de uso: el Asistente los ofrece como consultas SELECCIONABLES
    /// (MudSelect), filtradas por <see cref="RolesPermitidos"/>. Los ejemplos
    /// que terminan en «:» son prefijos que esperan texto pegado (p. ej.
    /// importación de contactos) y no se ofrecen como consulta completa.
    /// </summary>
    IReadOnlyCollection<string> Ejemplos => Array.Empty<string>();

    /// <summary>
    /// Palabras clave (en minúsculas y sin acentos) para el enrutamiento
    /// por intención. Se comparan como palabras completas.
    /// </summary>
    IReadOnlyCollection<string> Keywords { get; }

    /// <summary>
    /// Roles que pueden consultar esta skill (matriz real de permisos de las
    /// páginas: Contaduría → Admin/Gerencia/Contador, Usuarios/Auditoría/
    /// Contactos → Admin/Gerencia, operación diaria → todos). Vacío = cualquier
    /// rol autenticado. Fuera de este conjunto el Asistente rechaza la consulta
    /// con un mensaje inteligente y la registra en Auditoría como
    /// "ConsultaIArestringida" (detección temprana de intentos de acceso).
    /// </summary>
    IReadOnlyCollection<string> RolesPermitidos => Array.Empty<string>();

    /// <summary>Ejecuta la consulta y devuelve el resultado formateado en texto.</summary>
    Task<SkillOutput> ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Registro de skills: detecta la intención del usuario en un mensaje y
/// ejecuta la skill correspondiente sobre datos reales del sistema.
/// </summary>
public interface ISkillRegistry
{
    /// <summary>Skills registradas, en orden de prioridad.</summary>
    IReadOnlyList<ISkill> Skills { get; }

    /// <summary>
    /// Devuelve el resultado de la skill que casó con el mensaje, o null si
    /// ninguna skill aplica. Nunca lanza excepciones: los fallos se devuelven
    /// como <see cref="SkillMatch"/> con Success = false.
    /// </summary>
    Task<SkillMatch?> MatchAsync(string userMessage, CancellationToken cancellationToken = default);
}

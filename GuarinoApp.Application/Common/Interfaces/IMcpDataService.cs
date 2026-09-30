namespace GuarinoApp.Application.Common.Interfaces;

/// <summary>
/// Cliente de servidores MCP externos (la app actúa como CLIENTE MCP).
/// Orquestación determinista (ver specs/adr/003): la app decide qué consultar
/// según la consulta del usuario; solo lectura, contra una allowlist de
/// servidores, auditando cada llamada saliente.
/// </summary>
public interface IMcpDataService
{
    /// <summary>
    /// Devuelve el contexto externo relevante para la consulta del usuario.
    /// Nunca lanza excepciones: el chat jamás debe fallar por un servidor
    /// externo (degradación elegante, SPEC 003 FR-4). Vacío si no hay reglas
    /// aplicables o si todas las llamadas fallan.
    /// </summary>
    Task<IReadOnlyList<McpContextItem>> GetContextAsync(string userQuery, CancellationToken cancellationToken = default);
}

/// <summary>Dato externo obtenido de un servidor MCP (resultado de solo lectura).</summary>
public sealed class McpContextItem
{
    public string Server { get; init; } = string.Empty;

    public string Tool { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;

    public long LatencyMs { get; init; }
}

using System.ComponentModel.DataAnnotations;

namespace GuarinoApp.Infrastructure.Services;

/// <summary>
/// Configuración del cliente MCP (SPEC 003): allowlist de servidores externos
/// y reglas de orquestación determinista. Sin entrada en la allowlist, la app
/// jamás se conecta a un servidor; añadir uno no exige recompilar.
/// </summary>
public class McpOptions
    {
    public const string SectionName = "Mcp";

    public bool Enabled { get; set; } = true;

    /// <summary>Timeout total por llamada saliente (conexión + herramienta).</summary>
    [Range(1, 120)]
    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>Vigencia de la caché de resultados (evita repetir llamadas idénticas).</summary>
    [Range(0, 3600)]
    public int CacheSeconds { get; set; } = 300;

    /// <summary>Máximo de llamadas externas por consulta del usuario.</summary>
    [Range(1, 5)]
    public int MaxCallsPerQuery { get; set; } = 2;

    /// <summary>Tope de caracteres por resultado: el contexto total del modelo es acotado.</summary>
    [Range(100, 4000)]
    public int MaxCharsPerResult { get; set; } = 700;

    public List<McpServerConfig> Servers { get; set; } = new();

    public List<McpRuleConfig> Rules { get; set; } = new();
    }

/// <summary>
/// Servidor MCP externo permitido (allowlist). <see cref="Token"/> viaja como
/// header Authorization: Bearer; jamás se envía a ningún otro destino.
/// </summary>
public class McpServerConfig
    {
    public string Name { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string Token { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;
    }

/// <summary>
/// Regla determinista: si la consulta contiene algún disparador, se llama la
/// herramienta del servidor indicado con los argumentos provistos
/// ("{query}" se reemplaza por la consulta del usuario).
/// </summary>
public class McpRuleConfig
    {
    public string Server { get; set; } = string.Empty;

    public string Tool { get; set; } = string.Empty;

    public List<string> Triggers { get; set; } = new();

    public Dictionary<string, string> Args { get; set; } = new();
    }

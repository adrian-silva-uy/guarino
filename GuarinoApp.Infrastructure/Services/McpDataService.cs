using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using GuarinoApp.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace GuarinoApp.Infrastructure.Services;

/// <summary>
/// Cliente MCP de la app (SPEC 003): la app consulta servidores MCP externos
/// permitidos (allowlist) para enriquecer el chat con datos en vivo.
/// Orquestación determinista (ADR-003): reglas con palabras disparadoras que
/// apuntan a una herramienta concreta; el modelo no elige herramientas.
/// Solo lectura, caché corta, auditoría de cada llamada saliente y
/// degradación elegante: jamás lanza excepciones al llamador (FR-4).
/// </summary>
public sealed class McpDataService : IMcpDataService
    {
    private readonly IOptionsMonitor<McpOptions> _optionsMonitor;
    private readonly IMemoryCache _cache;
    private readonly IAuditService _audit;
    private readonly ILogger<McpDataService> _logger;

    public McpDataService(
        IOptionsMonitor<McpOptions> optionsMonitor,
        IMemoryCache cache,
        IAuditService audit,
        ILogger<McpDataService> logger)
        {
        _optionsMonitor = optionsMonitor;
        _cache = cache;
        _audit = audit;
        _logger = logger;
        }

    public async Task<IReadOnlyList<McpContextItem>> GetContextAsync(string userQuery, CancellationToken cancellationToken = default)
        {
        try
            {
            var options = _optionsMonitor.CurrentValue;
            if (!options.Enabled || string.IsNullOrWhiteSpace(userQuery))
                return Array.Empty<McpContextItem>();

            var matches = MatchRules(options, userQuery);
            if (matches.Count == 0)
                return Array.Empty<McpContextItem>();

            var results = new List<McpContextItem>();
            foreach (var match in matches.Take(options.MaxCallsPerQuery))
                {
                var item = await CallAsync(match.Server, match.Rule, userQuery, options, cancellationToken);
                if (item != null)
                    results.Add(item);
                }

            return results;
            }
        catch (Exception ex)
            {
            // FR-4: el chat nunca debe fallar por un servidor externo.
            _logger.LogWarning(ex, "MCP: error inesperado consultando contexto externo.");
            return Array.Empty<McpContextItem>();
            }
        }

    /// <summary>
    /// Reglas aplicables: el servidor debe estar en la allowlist y habilitado
    /// (FR-1/FR-6) y la consulta debe contener algún disparador.
    /// </summary>
    private static List<(McpServerConfig Server, McpRuleConfig Rule)> MatchRules(McpOptions options, string query)
        {
        var normalizedQuery = NormalizeText(query);
        var matches = new List<(McpServerConfig Server, McpRuleConfig Rule)>();

        foreach (var rule in options.Rules)
            {
            if (rule.Triggers.Count == 0 || string.IsNullOrWhiteSpace(rule.Tool))
                continue;

            var server = options.Servers.FirstOrDefault(s =>
                s.Enabled && string.Equals(s.Name, rule.Server, StringComparison.OrdinalIgnoreCase));
            if (server == null)
                continue;

            var triggered = rule.Triggers.Any(t => !string.IsNullOrWhiteSpace(t)
                && normalizedQuery.Contains(NormalizeText(t), StringComparison.Ordinal));
            if (triggered)
                matches.Add((server, rule));
            }

        return matches;
        }

    /// <summary>Llama una herramienta MCP. Devuelve null si falla (degradación elegante).</summary>
    private async Task<McpContextItem?> CallAsync(
        McpServerConfig server,
        McpRuleConfig rule,
        string query,
        McpOptions options,
        CancellationToken cancellationToken)
        {
        var args = BuildArgs(rule, query);
        var cacheKey = $"mcp:{server.Name}:{rule.Tool}:{JsonSerializer.Serialize(args)}";

        // Caché: no hubo llamada saliente, por eso no se audita este acierto.
        if (_cache.TryGetValue<McpContextItem>(cacheKey, out var cached) && cached != null)
            return cached;

        var sw = Stopwatch.StartNew();
        try
            {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(options.TimeoutSeconds, 1)));

            var transportOptions = new HttpClientTransportOptions
                {
                Endpoint = new Uri(server.Url),
                Name = server.Name,
                ConnectionTimeout = TimeSpan.FromSeconds(Math.Max(options.TimeoutSeconds, 1))
                };

            if (!string.IsNullOrWhiteSpace(server.Token))
                transportOptions.AdditionalHeaders = new Dictionary<string, string>
                    { ["Authorization"] = "Bearer " + server.Token };

            await using var transport = new HttpClientTransport(transportOptions);
            await using var client = await McpClient.CreateAsync(transport, cancellationToken: timeout.Token);

            var result = await client.CallToolAsync(rule.Tool, args, cancellationToken: timeout.Token);
            sw.Stop();

            var text = string.Join("\n",
                result.Content.OfType<TextContentBlock>().Select(block => block.Text ?? string.Empty));

            if (result.IsError == true || string.IsNullOrWhiteSpace(text))
                {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(text) ? "Respuesta vacía del servidor externo." : Truncate(text, 200));
                }

            var item = new McpContextItem
                {
                Server = server.Name,
                Tool = rule.Tool,
                Content = Truncate(text, options.MaxCharsPerResult),
                LatencyMs = sw.ElapsedMilliseconds
                };

            if (options.CacheSeconds > 0)
                _cache.Set(cacheKey, item, TimeSpan.FromSeconds(options.CacheSeconds));

            await AuditAsync(server, rule, args, query, ok: true, latencyMs: sw.ElapsedMilliseconds, error: null);
            return item;
            }
        catch (Exception ex)
            {
            sw.Stop();
            _logger.LogWarning("MCP: fallo consultando {Server}/{Tool} ({Latency} ms): {Error}",
                server.Name, rule.Tool, sw.ElapsedMilliseconds, ex.Message);
            await AuditAsync(server, rule, args, query, ok: false, latencyMs: sw.ElapsedMilliseconds, error: ex.Message);
            return null;
            }
        }

    private static Dictionary<string, object?> BuildArgs(McpRuleConfig rule, string query)
        {
        var args = new Dictionary<string, object?>();
        foreach (var pair in rule.Args)
            args[pair.Key] = pair.Value.Replace("{query}", query, StringComparison.OrdinalIgnoreCase);
        return args;
        }

    private Task AuditAsync(
        McpServerConfig server,
        McpRuleConfig rule,
        Dictionary<string, object?> args,
        string query,
        bool ok,
        long latencyMs,
        string? error)
        {
        var description = ok
            ? $"Consulta MCP {server.Name}/{rule.Tool}: OK ({latencyMs} ms)"
            : $"Consulta MCP {server.Name}/{rule.Tool}: ERROR ({latencyMs} ms) - {Truncate(error ?? string.Empty, 120)}";

        var additional = JsonSerializer.Serialize(new
            {
            server = server.Name,
            tool = rule.Tool,
            args,
            query = Truncate(query, 120),
            ok,
            latencyMs,
            error
            });

        return _audit.LogAsync("McpCall", "MCP", description, additional);
        }

    /// <summary>Minúsculas y sin acentos: "dólar" y "dolar" disparan lo mismo.</summary>
    private static string NormalizeText(string value)
        {
        var formD = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(formD.Length);
        foreach (var ch in formD)
            {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                builder.Append(ch);
            }
        return builder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max] + "…";
    }

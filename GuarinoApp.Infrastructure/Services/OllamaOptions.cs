using System.ComponentModel.DataAnnotations;

namespace GuarinoApp.Infrastructure.Services;

/// <summary>
/// Opciones de configuración para Ollama.
/// </summary>
public class OllamaOptions
    {
    public const string SectionName = "Ollama";

    public bool Enabled { get; set; }

    [Required(ErrorMessage = "Falta configurar 'Ollama:Url' en appsettings.json")]
    public string Url { get; set; } = string.Empty;

    [Required(ErrorMessage = "Falta configurar 'Ollama:Model' en appsettings.json")]
    public string Model { get; set; } = string.Empty;

    // Se aumentó el rango máximo a 600 segundos (10 min) para permitir timeouts largos
    [Range(30, 600, ErrorMessage = "'Ollama:TimeoutSeconds' debe estar entre 30 y 600 segundos")]
    public int TimeoutSeconds { get; set; } = 300; // Valor por defecto aumentado a 5 min

    [Range(5, 3600)]
    public int HealthCheckIntervalSeconds { get; set; } = 30;

    public int? NumCtx { get; set; } = 4096; // Aumentado por defecto para evitar truncamiento

    [Range(1, 200)]
    public int? TopK { get; set; } = 40;

    [Range(0.0, 1.0)]
    public double? TopP { get; set; } = 0.9;

    [Range(0.0, 2.0)]
    public double? Temperature { get; set; } = 0.2;

    // usado por keep_alive en los request a /api/chat ---
    public string KeepAlive { get; set; } = "30m";
    }

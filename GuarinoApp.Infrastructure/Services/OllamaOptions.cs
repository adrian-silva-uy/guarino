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

    // Auto-reparación silenciosa del servicio local (OllamaRecuperacionService):
    // si la generación falla con error 5xx (proceso "colgado") o el proceso deja
    // de responder, se reinicia solo, se verifica y se avisa por correo al
    // administrador con lo ocurrido y la solución. Solo actúa cuando Ollama:Url
    // es local (localhost) — en otra máquina no hay qué reiniciar desde acá.
    public OllamaSanacionOptions Sanacion { get; set; } = new();
    }

/// <summary>Opciones de Ollama:Sanacion (auto-reparación de la IA local).</summary>
public class OllamaSanacionOptions
    {
    public bool Habilitada { get; set; } = true;

    /// <summary>Cada cuánto se revisa si hubo fallos de la IA local a reparar.</summary>
    [Range(15, 3600)]
    public int IntervaloSegundos { get; set; } = 60;

    /// <summary>Tope de reinicios en una hora: si no se recupera, se avisa una vez
    /// que requiere intervención manual y no se reintenta hasta la próxima ventana
    /// (evita un bucle de reinicios contra un problema de fondo).</summary>
    [Range(1, 20)]
    public int MaxReiniciosPorHora { get; set; } = 3;

    /// <summary>False = solo detecta y avisa por correo (p. ej. si se quiere que el
    /// reinicio lo haga únicamente el gestor de servicios del servidor).</summary>
    public bool GestionarProceso { get; set; } = true;

    /// <summary>Ruta de ollama.exe. Vacío = se toma del proceso que está corriendo
    /// o se busca en el PATH del sistema/usuario. En producción puede dejarse
    /// vacío si Ollama está en el PATH, o fijarse explícitamente acá.</summary>
    public string RutaEjecutable { get; set; } = string.Empty;

    /// <summary>Tiempo máximo de la generación de prueba con la que se verifica
    /// (incluye la primera carga del modelo, del orden de 10 s).</summary>
    [Range(10, 300)]
    public int TimeoutVerificacionSegundos { get; set; } = 90;
    }

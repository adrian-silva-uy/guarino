namespace GuarinoApp.Infrastructure.Services;

/// <summary>
/// Estado compartido y en memoria de la conexión a Ollama.
///
/// Esta clase es el puente:
/// se registra como Singleton, OllamaStartupService la actualiza cuando
/// confirma que Ollama y el modelo están listos, y OllamaAIService la
/// consulta antes de intentar una llamada.
/// </summary>
public class OllamaConnectionState
    {
    public bool IsConnected { get; set; }
    public bool IsModelReady { get; set; }

    /// <summary>True solo si Ollama respondió Y el modelo configurado está descargado.</summary>
    public bool IsReady => IsConnected && IsModelReady;

    /// <summary>Fecha/hora (UTC) del último fallo REAL de generación reportado por
    /// OllamaAIService (HTTP 5xx, red, excepción). Lo consume
    /// OllamaRecuperacionService para verificar y reparar el servicio; nulo = sin
    /// fallos desde el arranque.</summary>
    public DateTime? UltimoFalloEn { get; set; }

    /// <summary>Motivo del último fallo de generación (ej. "HTTP 500",
    /// "HttpRequestException").</summary>
    public string? UltimoFalloMotivo { get; set; }
    }

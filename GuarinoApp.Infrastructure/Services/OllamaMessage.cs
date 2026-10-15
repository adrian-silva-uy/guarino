using System.Text.Json.Serialization;

namespace GuarinoApp.Infrastructure.Services;

/// <summary>Mensaje dentro de una respuesta de Ollama (/api/chat).</summary>
public class OllamaMessage
    {
    [JsonPropertyName("content")]
    public string? Content { get; set; }
    }

using System.Text.Json.Serialization;

namespace GuarinoApp.Infrastructure.Services;

/// <summary>
/// Sobre de respuesta de Ollama para /api/chat: modelo propio de la
/// comunicación con el servicio externo (formato wire), separado del servicio
/// por una responsabilidad por archivo.
/// </summary>
public class OllamaChatResponse
    {
    [JsonPropertyName("message")]
    public OllamaMessage? Message { get; set; }
    }

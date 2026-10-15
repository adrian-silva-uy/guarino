namespace GuarinoApp.Infrastructure.Services;

/// <summary>Entrada del caché de resultados IA (<see cref="AiResultCache"/>): valor + vencimiento.</summary>
internal sealed class AiResultCacheEntrada
    {
    public DateTime Expira { get; init; }
    public object? Valor { get; init; }
    }

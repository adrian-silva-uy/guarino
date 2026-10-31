using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace GuarinoApp.Infrastructure.Services;

/// <summary>
/// Caché distribuida con Redis como backend principal y MemoryCache como fallback.
/// Si Redis no está disponible, se usa memoria en proceso sin fallar la aplicación.
/// </summary>
public sealed class DistributedCacheService : IDistributedCacheService
{
    private readonly IDistributedCache _redis;
    private readonly IMemoryCache _memory;
    private readonly bool _redisAvailable;

    public DistributedCacheService(IDistributedCache redis, IMemoryCache memory, IOptions<CacheOptions> options)
    {
        _redis = redis;
        _memory = memory;
        _redisAvailable = options.Value.Enabled;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        // 1. Intentar Redis
        if (_redisAvailable)
        {
            try
            {
                var raw = await _redis.GetStringAsync(key, ct);
                if (raw != null)
                    return JsonSerializer.Deserialize<T>(raw);
            }
            catch
            {
                // Redis no disponible, caer a memoria
            }
        }

        // 2. Fallback a memoria
        if (_memory.TryGetValue(key, out var mem))
            return (T)mem;

        return default;
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        var redisOpts = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiry ?? TimeSpan.FromMinutes(30)
        };
        var memOpts = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiry ?? TimeSpan.FromMinutes(30)
        };

        if (_redisAvailable)
        {
            try
            {
                var raw = JsonSerializer.Serialize(value);
                await _redis.SetStringAsync(key, raw, redisOpts, ct);
            }
            catch
            {
                // Redis no disponible, guardar solo en memoria
            }
        }

        _memory.Set(key, value, memOpts);
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        if (_redisAvailable)
        {
            try
            {
                await _redis.RemoveAsync(key, ct);
            }
            catch { }
        }

        _memory.Remove(key);
    }
}

public interface IDistributedCacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
}

public class CacheOptions
{
    public bool Enabled { get; set; } = true;
    public string ConnectionString { get; set; } = "localhost:6379";
    public string InstanceName { get; set; } = "guarino:";
}

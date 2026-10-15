using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace VeriPass.Infrastructure.Caching;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}

public class RedisCacheService : ICacheService
{
    private readonly Dictionary<string, (string Value, DateTime? Expiry)> _inMemoryFallback = new();

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        lock (_inMemoryFallback)
        {
            if (_inMemoryFallback.TryGetValue(key, out var item))
            {
                if (item.Expiry.HasValue && item.Expiry.Value < DateTime.UtcNow)
                {
                    _inMemoryFallback.Remove(key);
                    return Task.FromResult<T?>(default);
                }

                var deserialized = JsonSerializer.Deserialize<T>(item.Value);
                return Task.FromResult(deserialized);
            }
        }
        return Task.FromResult<T?>(default);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        lock (_inMemoryFallback)
        {
            var json = JsonSerializer.Serialize(value);
            var expiryDate = expiry.HasValue ? DateTime.UtcNow.Add(expiry.Value) : (DateTime?)null;
            _inMemoryFallback[key] = (json, expiryDate);
        }
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        lock (_inMemoryFallback)
        {
            _inMemoryFallback.Remove(key);
        }
        return Task.CompletedTask;
    }
}

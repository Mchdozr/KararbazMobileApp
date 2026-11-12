using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Nereye.Application.Places;
using Nereye.Domain.Places;

namespace Nereye.Infrastructure.Places;

public sealed class DistributedPlacesCache : IPlacesCache
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly IDistributedCache _cache;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new();

    public DistributedPlacesCache(IDistributedCache cache) => _cache = cache;

    public async Task<T> GetOrSetAsync<T>(
        string key,
        TimeSpan ttl,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken = default)
    {
        var existing = await TryGetAsync<T>(key, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var gate = _gates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            existing = await TryGetAsync<T>(key, cancellationToken);
            if (existing is not null)
            {
                return existing;
            }

            var created = await factory(cancellationToken);
            await SetAsync(key, ttl, created, cancellationToken);
            return created;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<T?> TryGetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var existing = await _cache.GetStringAsync(key, cancellationToken);
        return existing is null ? default : JsonSerializer.Deserialize<T>(existing, Json);
    }

    public async Task SetAsync<T>(string key, TimeSpan ttl, T value, CancellationToken cancellationToken = default)
    {
        if (value is null)
        {
            return;
        }

        var empty = value is System.Collections.ICollection { Count: 0 };
        await _cache.SetStringAsync(
            key,
            JsonSerializer.Serialize(value, Json),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = empty ? PlacesCacheTtl.Empty : ttl },
            cancellationToken);
    }
}

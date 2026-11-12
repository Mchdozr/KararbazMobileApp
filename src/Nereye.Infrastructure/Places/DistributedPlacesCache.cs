using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Nereye.Application.Places;

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
        var existing = await _cache.GetStringAsync(key, cancellationToken);
        if (existing is not null)
        {
            return JsonSerializer.Deserialize<T>(existing, Json)!;
        }

        var gate = _gates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            existing = await _cache.GetStringAsync(key, cancellationToken);
            if (existing is not null)
            {
                return JsonSerializer.Deserialize<T>(existing, Json)!;
            }

            var created = await factory(cancellationToken);
            if (created is null || created is System.Collections.ICollection { Count: 0 })
            {
                return created;
            }

            var payload = JsonSerializer.Serialize(created, Json);
            await _cache.SetStringAsync(
                key,
                payload,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl },
                cancellationToken);
            return created;
        }
        finally
        {
            gate.Release();
        }
    }
}

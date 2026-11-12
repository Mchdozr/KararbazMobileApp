using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Nereye.Application.Places;
using Nereye.Domain.Places;

namespace Nereye.Infrastructure.Places;

/// <summary>
/// İki katmanlı önbellek. Önce süreç belleği (ağ ve JSON çözme yok), sonra paylaşılan dağıtık önbellek (Redis).
/// Aynı anahtarı aynı anda isteyenler tek bir dış çağrıyı bekler; bu yüzden tekil (singleton) kayıtlıdır.
/// Bellekteki nesneler istekler arasında paylaşılır: çağıranlar dönen değeri değiştirmemelidir.
/// </summary>
public sealed class DistributedPlacesCache : IPlacesCache
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan LocalTtl = TimeSpan.FromMinutes(5);
    private readonly IDistributedCache _cache;
    private readonly IMemoryCache _local;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new();

    public DistributedPlacesCache(IDistributedCache cache, IMemoryCache? local = null)
    {
        _cache = cache;
        _local = local ?? new MemoryCache(new MemoryCacheOptions());
    }

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
            // Bekleyen yoksa kapıyı bırak: her mekan kimliği için kalıcı bir semafor birikmesin.
            if (gate.CurrentCount == 1)
            {
                _gates.TryRemove(new KeyValuePair<string, SemaphoreSlim>(key, gate));
            }
        }
    }

    public async Task<T?> TryGetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (_local.TryGetValue(key, out var hit) && hit is T typed)
        {
            return typed;
        }

        string? raw;
        try
        {
            raw = await _cache.GetStringAsync(key, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Redis erişilemiyorsa önbelleksiz devam: yavaşlar ama çalışır.
            return default;
        }

        if (raw is null)
        {
            return default;
        }

        var value = JsonSerializer.Deserialize<T>(raw, Json);
        if (value is not null)
        {
            _local.Set(key, value, LocalTtl);
        }

        return value;
    }

    public async Task SetAsync<T>(string key, TimeSpan ttl, T value, CancellationToken cancellationToken = default)
    {
        if (value is null)
        {
            return;
        }

        var empty = value is System.Collections.ICollection { Count: 0 };
        var life = empty ? PlacesCacheTtl.Empty : ttl;
        _local.Set(key, value, life < LocalTtl ? life : LocalTtl);
        try
        {
            await _cache.SetStringAsync(
                key,
                JsonSerializer.Serialize(value, Json),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = life },
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            /* yalnızca bellekte kalır */
        }
    }
}

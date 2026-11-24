using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nereye.Application.Common;
using Nereye.Application.Places;
using Nereye.Domain.Places;

namespace Nereye.Infrastructure.Places;

/// <summary>
/// "Bayatken sun, arkada tazele". Kayıtların çekildiği anı ayrı bir damga anahtarında tutar; yumuşak süre
/// dolmuş bir kayıt sunulurken aynı anahtar için tek bir arka plan tazelemesi başlatır.
/// Tazeleme başarısız ya da boş dönerse eski kayıt korunur ve kısa bir süre yeniden denenmez.
/// Tekil (singleton) kayıtlıdır: süreç içinde aynı anahtar için iki tazeleme koşmaz.
/// </summary>
public sealed class CacheRefresher
{
    private readonly ConcurrentDictionary<string, Task> _running = new();
    private readonly IPlacesCache _cache;
    private readonly IClock _clock;
    private readonly ILogger<CacheRefresher>? _log;
    private readonly CancellationToken _stopping;

    public CacheRefresher(
        IPlacesCache cache,
        IClock clock,
        ILogger<CacheRefresher>? log = null,
        IHostApplicationLifetime? lifetime = null)
    {
        _cache = cache;
        _clock = clock;
        _log = log;
        _stopping = lifetime?.ApplicationStopping ?? CancellationToken.None;
    }

    public async Task<bool> IsStaleAsync(string key, TimeSpan softTtl, CancellationToken cancellationToken = default)
    {
        var raw = await _cache.TryGetAsync<string>(PlacesCacheKey.Stamp(key), cancellationToken);
        DateTime? at = DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed.ToUniversalTime()
            : null;
        return CacheFreshness.IsStale(at, _clock.UtcNow, softTtl);
    }

    /// <summary>Kaydın şimdi çekildiğini işaretler. Damga kayıtla aynı süre yaşar.</summary>
    public Task MarkFreshAsync(string key, TimeSpan ttl, CancellationToken cancellationToken = default) =>
        WriteStampAsync(key, ttl, _clock.UtcNow, cancellationToken);

    /// <summary>
    /// Kayıt bayatsa arka planda tazelemeyi başlatır ve hemen döner. fetch'in sonucu keep'ten geçerse
    /// kayıt ve damga yazılır, ardından afterStore (ör. detay önbelleği) çalışır.
    /// </summary>
    public async Task RevalidateIfStaleAsync<T>(
        string key,
        TimeSpan ttl,
        TimeSpan softTtl,
        Func<CancellationToken, Task<T>> fetch,
        Func<T, bool> keep,
        Func<T, CancellationToken, Task>? afterStore = null,
        CancellationToken cancellationToken = default)
    {
        if (_running.ContainsKey(key) || !await IsStaleAsync(key, softTtl, cancellationToken))
        {
            return;
        }

        TryStart(key, async ct =>
        {
            if (!await RefreshAsync(key, ttl, fetch, keep, afterStore, ct))
            {
                await WriteStampAsync(key, ttl, CacheFreshness.RetryStamp(_clock.UtcNow, softTtl, PlacesCacheTtl.Empty), ct);
            }
        });
    }

    /// <summary>Kaydı hemen (beklenerek) tazeler. Isıtma işi kullanır. Yazıldıysa true.</summary>
    public async Task<bool> RefreshAsync<T>(
        string key,
        TimeSpan ttl,
        Func<CancellationToken, Task<T>> fetch,
        Func<T, bool> keep,
        Func<T, CancellationToken, Task>? afterStore = null,
        CancellationToken cancellationToken = default)
    {
        T value;
        try
        {
            value = await fetch(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _log?.LogWarning(ex, "Önbellek tazelemesi başarısız: {Key}", key);
            return false;
        }

        if (value is null || !keep(value))
        {
            return false;
        }

        await _cache.SetAsync(key, ttl, value, cancellationToken);
        await MarkFreshAsync(key, ttl, cancellationToken);
        if (afterStore is not null)
        {
            await afterStore(value, cancellationToken);
        }

        return true;
    }

    /// <summary>Testler için: başlamış tazelemelerin bitmesini bekler.</summary>
    internal async Task WhenIdleAsync()
    {
        while (!_running.IsEmpty)
        {
            await Task.WhenAll(_running.Values.ToArray());
        }
    }

    private void TryStart(string key, Func<CancellationToken, Task> work)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_running.TryAdd(key, done.Task))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await work(_stopping);
            }
            catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
            {
                /* uygulama kapanıyor */
            }
            catch (Exception ex)
            {
                _log?.LogWarning(ex, "Arka plan tazelemesi durdu: {Key}", key);
            }
            finally
            {
                _running.TryRemove(key, out _);
                done.TrySetResult();
            }
        });
    }

    private Task WriteStampAsync(string key, TimeSpan ttl, DateTime at, CancellationToken cancellationToken) =>
        _cache.SetAsync(PlacesCacheKey.Stamp(key), ttl, at.ToString("O", CultureInfo.InvariantCulture), cancellationToken);
}

namespace Nereye.Infrastructure.Places;

/// <summary>Kapı doluyken beklemek yerine vazgeçildi; çağıran bunu "servis yanıt vermedi" gibi ele alır.</summary>
public sealed class NominatimBusyException(string message) : HttpRequestException(message);

/// <summary>
/// Public Nominatim kullanım politikası saniyede en fazla bir istek ister. Bu kapı tüm süreç için tektir
/// (tekil kayıtlı): iki istek arasında, başlangıçtan başlangıca en az <see cref="Interval"/> geçer ve
/// istekler sırayla gider. İstemcinin debounce'una güvenilmez; kuralı burası uygular.
/// </summary>
public sealed class NominatimGate
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly TimeProvider _time;
    private DateTimeOffset _lastStart = DateTimeOffset.MinValue;

    public NominatimGate(TimeSpan? interval = null, TimeProvider? time = null)
    {
        Interval = interval ?? TimeSpan.FromSeconds(1);
        _time = time ?? TimeProvider.System;
    }

    public TimeSpan Interval { get; }

    /// <summary>
    /// <paramref name="call"/>'ı sıraya alır. Sıra <paramref name="maxWait"/> içinde gelmezse
    /// <see cref="NominatimBusyException"/> fırlatır: otomatik tamamlama uzun kuyrukta beklemesin.
    /// </summary>
    public async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> call, TimeSpan maxWait, CancellationToken cancellationToken)
    {
        var started = _time.GetUtcNow();
        if (!await _lock.WaitAsync(maxWait, cancellationToken))
        {
            throw new NominatimBusyException("Nominatim sırası dolu.");
        }

        try
        {
            var wait = _lastStart + Interval - _time.GetUtcNow();
            if (wait > TimeSpan.Zero)
            {
                if (_time.GetUtcNow() - started + wait > maxWait)
                {
                    throw new NominatimBusyException("Nominatim sırası dolu.");
                }

                await Task.Delay(wait, _time, cancellationToken);
            }

            _lastStart = _time.GetUtcNow();
            return await call(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }
}

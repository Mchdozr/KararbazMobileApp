namespace Nereye.Application.Places;

public interface IPlacesCache
{
    Task<T> GetOrSetAsync<T>(string key, TimeSpan ttl, Func<CancellationToken, Task<T>> factory, CancellationToken cancellationToken = default);

    Task<T?> TryGetAsync<T>(string key, CancellationToken cancellationToken = default);

    Task SetAsync<T>(string key, TimeSpan ttl, T value, CancellationToken cancellationToken = default);
}

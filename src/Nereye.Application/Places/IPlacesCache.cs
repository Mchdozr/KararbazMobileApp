namespace Nereye.Application.Places;

public interface IPlacesCache
{
    Task<T> GetOrSetAsync<T>(string key, TimeSpan ttl, Func<CancellationToken, Task<T>> factory, CancellationToken cancellationToken = default);
}

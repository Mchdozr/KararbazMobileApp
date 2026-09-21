using Nereye.Domain.Places;

namespace Nereye.Application.Places;

public interface IGeoSuggest
{
    Task<IReadOnlyList<GeoHit>> SuggestAsync(string query, CancellationToken cancellationToken = default);
}

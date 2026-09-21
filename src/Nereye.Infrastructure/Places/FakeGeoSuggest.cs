using Nereye.Application.Places;
using Nereye.Domain.Places;

namespace Nereye.Infrastructure.Places;

public sealed class FakeGeoSuggest : IGeoSuggest
{
    internal static readonly GeoHit[] Seed =
    [
        new("Kadıköy", 40.9885, 29.025, "suburb"),
        new("Moda", 40.9808, 29.0261, "suburb"),
        new("Halide Edip Adıvar", 41.055, 28.987, "suburb"),
        new("Nişantaşı", 41.0489, 28.994, "suburb"),
        new("Osmanbey", 41.0524, 28.9878, "suburb"),
        new("Karaköy", 41.0234, 28.973, "suburb"),
        new("Cihangir", 41.0324, 28.9865, "suburb"),
        new("Beşiktaş", 41.0422, 29.0078, "suburb")
    ];

    public Task<IReadOnlyList<GeoHit>> SuggestAsync(string query, CancellationToken cancellationToken = default)
    {
        var q = query.Trim();
        IReadOnlyList<GeoHit> hits = q.Length < 2
            ? []
            : Seed.Where(s => s.Label.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        return Task.FromResult(hits);
    }
}

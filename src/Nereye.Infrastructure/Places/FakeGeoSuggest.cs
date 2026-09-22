using Nereye.Application.Places;
using Nereye.Domain.Places;

namespace Nereye.Infrastructure.Places;

public sealed class FakeGeoSuggest : IGeoSuggest
{
    internal static readonly GeoHit[] Seed =
    [
        new("Kadıköy", 40.9885, 29.025, "suburb", "İstanbul", "Mahalle", "Kadıköy, İstanbul"),
        new("Moda", 40.9808, 29.0261, "suburb", "Kadıköy · İstanbul", "Mahalle", "Moda, Kadıköy"),
        new("Halide Edip Adıvar", 41.055, 28.987, "suburb", "Şişli · İstanbul", "Mahalle", "Halide Edip Adıvar, Şişli"),
        new("Nişantaşı", 41.0489, 28.994, "suburb", "Şişli · İstanbul", "Mahalle", "Nişantaşı, Şişli"),
        new("Osmanbey", 41.0524, 28.9878, "suburb", "Şişli · İstanbul", "Mahalle", "Osmanbey, Şişli"),
        new("Karaköy", 41.0234, 28.973, "suburb", "Beyoğlu · İstanbul", "Mahalle", "Karaköy, Beyoğlu"),
        new("Cihangir", 41.0324, 28.9865, "suburb", "Beyoğlu · İstanbul", "Mahalle", "Cihangir, Beyoğlu"),
        new("Beşiktaş", 41.0422, 29.0078, "city_district", "İstanbul", "İlçe", "Beşiktaş, İstanbul")
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

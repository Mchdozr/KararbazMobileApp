namespace Nereye.Domain.Places;

/// <summary>
/// Önbellek süreleri. Google içeriği taşıyan kayıtlar (Nearby, Rich, PhotoUri) Google Maps Platform
/// önbellek şartına tabidir; şart metni doğrulanamadı (docs/yayin-riskleri.md). Bu yüzden Google'a
/// dayanan süreler kısa tutuldu ve yalnızca OpenStreetMap verisi uzun saklanıyor.
/// </summary>
public static class PlacesCacheTtl
{
    /// <summary>Liste (OSM + Google birleşimi).</summary>
    public static readonly TimeSpan Nearby = TimeSpan.FromHours(6);

    /// <summary>Tek mekan detayı (Google özet, yorum, olanak).</summary>
    public static readonly TimeSpan Rich = TimeSpan.FromHours(24);

    /// <summary>
    /// Kapak görseli. Google karesi (/api/media/google) Rich ile aynı süre tutulur; Wikimedia, web sitesi
    /// ve işletme görselleri daha uzun.
    /// </summary>
    public static readonly TimeSpan CoverGoogle = TimeSpan.FromHours(24);

    public static readonly TimeSpan Cover = TimeSpan.FromDays(7);

    /// <summary>
    /// Google'ın fotoğraf için döndürdüğü indirme adresi kısa ömürlü olabilir ve saklanması şartla
    /// sınırlı olabilir; ömrü doğrulanamadığı için kısa tutulur.
    /// </summary>
    public static readonly TimeSpan PhotoUri = TimeSpan.FromMinutes(30);

    /// <summary>Yalnızca OSM verisi: politikası önbelleği teşvik eder; Google içeriği yok.</summary>
    public static readonly TimeSpan OsmNearby = TimeSpan.FromDays(3);

    public static readonly TimeSpan OsmDetails = TimeSpan.FromDays(7);

    /// <summary>Nominatim konum önerisi.</summary>
    public static readonly TimeSpan Suggest = TimeSpan.FromDays(7);

    public static readonly TimeSpan Empty = TimeSpan.FromMinutes(2);

    /// <summary>Yürüme/araç rotası: sokak ağı nadiren değişir; trafik verisi kullanılmıyor.</summary>
    public static readonly TimeSpan Route = TimeSpan.FromHours(12);
}

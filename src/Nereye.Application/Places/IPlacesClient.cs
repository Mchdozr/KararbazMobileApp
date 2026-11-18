using Nereye.Domain.Places;

namespace Nereye.Application.Places;

public interface IPlacesClient
{
    Task<IReadOnlyList<PlaceSnapshot>> NearbyAsync(
        double latitude,
        double longitude,
        Intent intent,
        int radiusMeters,
        CancellationToken cancellationToken = default);

    /// <summary>Kart ve liste için yeten kayıt. Google'ın zengin (Atmosphere) alanlarını istemez.</summary>
    Task<PlaceSnapshot?> DetailsAsync(string placeId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlaceSnapshot>> TextSearchAsync(
        string query,
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Yalnızca tek mekan detay sayfası için: özet, yorumlar, olanaklar, galeri. Google'ın en pahalı
/// alanlarını istediği için liste, favori ya da toplu fotoğraf akışlarında çağrılmamalı.
/// </summary>
public interface IRichPlaceDetails
{
    Task<PlaceSnapshot?> RichDetailsAsync(string placeId, CancellationToken cancellationToken = default);
}

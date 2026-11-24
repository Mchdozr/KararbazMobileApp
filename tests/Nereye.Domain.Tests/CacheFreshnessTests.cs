using Nereye.Domain.Places;

namespace Nereye.Domain.Tests;

public class CacheFreshnessTests
{
    private static readonly DateTime Now = new(2026, 11, 24, 18, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Entry_younger_than_soft_ttl_is_fresh()
    {
        Assert.False(CacheFreshness.IsStale(Now.AddHours(-1), Now, TimeSpan.FromHours(4)));
    }

    [Fact]
    public void Entry_at_or_past_soft_ttl_is_stale()
    {
        Assert.True(CacheFreshness.IsStale(Now.AddHours(-4), Now, TimeSpan.FromHours(4)));
        Assert.True(CacheFreshness.IsStale(Now.AddHours(-5), Now, TimeSpan.FromHours(4)));
    }

    [Fact]
    public void Missing_stamp_counts_as_stale()
    {
        // Damga yoksa kayıt eski sürümden kalmıştır ya da damga düşmüştür: bir kez tazelemek güvenli.
        Assert.True(CacheFreshness.IsStale(null, Now, TimeSpan.FromHours(4)));
    }

    [Fact]
    public void Stamp_from_the_future_is_fresh()
    {
        // Sunucular arası saat kayması tazelemeyi tetiklemesin.
        Assert.False(CacheFreshness.IsStale(Now.AddMinutes(3), Now, TimeSpan.FromHours(4)));
    }

    [Fact]
    public void Retry_stamp_becomes_stale_after_the_retry_delay()
    {
        var soft = TimeSpan.FromHours(4);
        var retryStamp = CacheFreshness.RetryStamp(Now, soft, TimeSpan.FromMinutes(2));
        Assert.False(CacheFreshness.IsStale(retryStamp, Now.AddMinutes(1), soft));
        Assert.True(CacheFreshness.IsStale(retryStamp, Now.AddMinutes(2), soft));
    }

    [Fact]
    public void Soft_ttls_leave_room_before_hard_expiry()
    {
        Assert.True(PlacesCacheTtl.NearbySoft < PlacesCacheTtl.Nearby);
        Assert.True(PlacesCacheTtl.OsmNearbySoft < PlacesCacheTtl.OsmNearby);
    }

    [Fact]
    public void Stamp_key_is_derived_from_the_entry_key()
    {
        Assert.Equal("stamp:v1:places:nearby:v6:sxk967:kahve:5000",
            PlacesCacheKey.Stamp(PlacesCacheKey.Nearby("sxk967", Intent.Kahve, 5000)));
    }
}

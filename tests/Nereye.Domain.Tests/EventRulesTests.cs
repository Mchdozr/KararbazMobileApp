using Nereye.Domain.Events;
using Nereye.Domain.Geo;

namespace Nereye.Domain.Tests;

public class EventRulesTests
{
    private static DateTime Utc(int day, int hour, int minute = 0) => new(2026, 11, day, hour, minute, 0, DateTimeKind.Utc);

    [Fact]
    public void Evening_window_runs_until_four_in_the_morning_istanbul_time()
    {
        // 18:00 İstanbul = 15:00Z → ertesi gün 04:00 İstanbul = 01:00Z
        var (from, to) = EventRules.TonightWindow(Utc(24, 15));
        Assert.Equal(Utc(24, 15), from);
        Assert.Equal(Utc(25, 1), to);
    }

    [Fact]
    public void After_midnight_the_same_night_continues()
    {
        // 02:00 İstanbul (25 Kasım) = 23:00Z (24 Kasım) → aynı gece 04:00 = 01:00Z
        var (_, to) = EventRules.TonightWindow(Utc(24, 23));
        Assert.Equal(Utc(25, 1), to);
    }

    [Fact]
    public void Morning_looks_ahead_to_tonight()
    {
        // 09:00 İstanbul = 06:00Z → ertesi gün 04:00 = 01:00Z (26 Kasım)
        var (_, to) = EventRules.TonightWindow(Utc(25, 6));
        Assert.Equal(Utc(26, 1), to);
    }

    [Fact]
    public void Tonight_includes_upcoming_and_ongoing_events_only()
    {
        var now = Utc(24, 15);
        Assert.True(EventRules.IsTonight(Utc(24, 17, 30), Utc(24, 19, 30), now));   // 20:30 başlıyor
        Assert.True(EventRules.IsTonight(Utc(24, 14), Utc(24, 16), now));            // şu an sürüyor
        Assert.False(EventRules.IsTonight(Utc(24, 12), Utc(24, 14), now));           // bitti
        Assert.False(EventRules.IsTonight(Utc(26, 17), null, now));                  // iki gün sonra
        Assert.True(EventRules.IsTonight(Utc(24, 14), null, now));                   // bitişi yok, 1 sa önce başladı
        Assert.False(EventRules.IsTonight(Utc(24, 11), null, now));                  // bitişi yok, 4 sa önce başladı
    }

    [Theory]
    [InlineData("Caz gecesi", 2, 2, null)]
    [InlineData("Ca", 2, 2, "Etkinlik adı 3–80 karakter olmalı.")]
    [InlineData("Caz gecesi", -1, 2, "Başlangıç geçmişte olamaz.")]
    [InlineData("Caz gecesi", 2, 0, "Bitiş başlangıçtan sonra olmalı.")]
    [InlineData("Caz gecesi", 2, 13, "Etkinlik en fazla 12 saat sürebilir.")]
    [InlineData("Caz gecesi", 24 * 91, 2, "En fazla 90 gün sonrası için etkinlik eklenebilir.")]
    public void Owner_event_validation(string title, int startsInHours, int durationHours, string? error)
    {
        var now = Utc(24, 15);
        var start = now.AddHours(startsInHours);
        Assert.Equal(error, EventRules.Validate(title, start, start.AddHours(durationHours), now));
    }

    [Fact]
    public void Geohash_decodes_back_to_the_cell_center()
    {
        var (lat, lng) = Geohash.Decode(Geohash.Encode(40.9885, 29.0250, 9));
        Assert.InRange(lat, 40.9884, 40.9886);
        Assert.InRange(lng, 29.0249, 29.0251);
    }
}

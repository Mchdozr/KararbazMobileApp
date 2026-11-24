using Nereye.Domain.Places;

namespace Nereye.Domain.Tests;

public class OpeningScheduleTests
{
    // İstanbul UTC+3 (yaz saati yok). Yerel saat verip UTC anına çeviren yardımcı.
    private static DateTimeOffset Ist(int y, int mo, int d, int h, int mi) =>
        new DateTimeOffset(y, mo, d, h, mi, 0, TimeSpan.FromHours(3)).ToUniversalTime();

    [Fact]
    public void Weekday_range_open_with_closing_time()
    {
        var s = OpeningSchedule.Evaluate("Mo-Fr 08:00-18:00", Ist(2026, 11, 23, 10, 0)); // Pazartesi
        Assert.True(s.IsOpen);
        Assert.Equal("18:00", s.ClosesAt);
        Assert.Equal(480, s.ClosesInMinutes);
    }

    [Fact]
    public void Closed_day_reports_next_opening()
    {
        var s = OpeningSchedule.Evaluate("Mo-Fr 08:00-18:00", Ist(2026, 11, 22, 12, 0)); // Pazar
        Assert.False(s.IsOpen);
        Assert.Equal("08:00", s.OpensAt);
        Assert.Equal(20 * 60, s.OpensInMinutes);
    }

    [Fact]
    public void Overnight_range_counts_after_midnight()
    {
        const string spec = "Fr-Sa 18:00-02:00";
        var lateFriday = OpeningSchedule.Evaluate(spec, Ist(2026, 11, 28, 1, 0)); // Cumartesi 01:00, cuma gecesinin devamı
        Assert.True(lateFriday.IsOpen);
        Assert.Equal("02:00", lateFriday.ClosesAt);
        Assert.Equal(60, lateFriday.ClosesInMinutes);

        var morning = OpeningSchedule.Evaluate(spec, Ist(2026, 11, 28, 3, 0));
        Assert.False(morning.IsOpen);
        Assert.Equal("18:00", morning.OpensAt);
        Assert.Equal(15 * 60, morning.OpensInMinutes);
    }

    [Fact]
    public void Rule_without_days_means_every_day()
    {
        var s = OpeningSchedule.Evaluate("09:00-23:00", Ist(2026, 11, 24, 22, 30)); // Salı
        Assert.True(s.IsOpen);
        Assert.Equal(30, s.ClosesInMinutes);
    }

    [Fact]
    public void Several_ranges_in_one_rule()
    {
        var lunch = OpeningSchedule.Evaluate("Mo-Fr 09:00-12:00,13:00-18:00", Ist(2026, 11, 23, 12, 30));
        Assert.False(lunch.IsOpen);
        Assert.Equal("13:00", lunch.OpensAt);
        Assert.Equal(30, lunch.OpensInMinutes);
    }

    [Fact]
    public void Midnight_written_as_24_00()
    {
        var s = OpeningSchedule.Evaluate("Mo-Su 10:00-24:00", Ist(2026, 11, 23, 23, 30));
        Assert.True(s.IsOpen);
        Assert.Equal("00:00", s.ClosesAt);
        Assert.Equal(30, s.ClosesInMinutes);
    }

    [Fact]
    public void Later_rule_overrides_days_and_holidays_are_ignored()
    {
        const string spec = "Mo-Fr 08:00-18:00; We off; PH off";
        var wednesday = OpeningSchedule.Evaluate(spec, Ist(2026, 11, 25, 10, 0));
        Assert.False(wednesday.IsOpen);
        Assert.Equal("08:00", wednesday.OpensAt);
        Assert.Equal(22 * 60, wednesday.OpensInMinutes); // perşembe 08:00

        Assert.True(OpeningSchedule.Evaluate(spec, Ist(2026, 11, 26, 10, 0)).IsOpen);
    }

    [Fact]
    public void Twenty_four_seven_has_no_closing_time()
    {
        var s = OpeningSchedule.Evaluate("24/7", Ist(2026, 11, 23, 3, 0));
        Assert.True(s.IsOpen);
        Assert.Null(s.ClosesAt);
    }

    [Fact]
    public void Evaluated_in_istanbul_time_not_server_time()
    {
        // 19:30 UTC = 22:30 İstanbul: sunucu saatine göre açık görünürdü, gerçekte kapalı.
        var now = new DateTimeOffset(2026, 11, 24, 19, 30, 0, TimeSpan.Zero);
        var s = OpeningSchedule.Evaluate("Mo-Su 10:00-22:00", now);
        Assert.False(s.IsOpen);
        Assert.Equal("10:00", s.OpensAt);
        Assert.Equal(11 * 60 + 30, s.OpensInMinutes);
    }

    [Fact]
    public void Google_turkish_weekday_lines()
    {
        const string text = "Pazartesi: 09:00–23:00; Salı: 24 saat açık; Çarşamba: Kapalı; Perşembe: 09:00–14:00, 16:00–23:00; "
                            + "Cuma: 09:00–02:00; Cumartesi: 10:00–02:00; Pazar: 10:00–22:00";
        Assert.True(OpeningSchedule.Evaluate(text, Ist(2026, 11, 24, 3, 0)).IsOpen);   // salı 24 saat

        var wednesday = OpeningSchedule.Evaluate(text, Ist(2026, 11, 25, 12, 0));
        Assert.False(wednesday.IsOpen);
        Assert.Equal("09:00", wednesday.OpensAt);

        var thursdayBreak = OpeningSchedule.Evaluate(text, Ist(2026, 11, 26, 15, 0));
        Assert.False(thursdayBreak.IsOpen);
        Assert.Equal(60, thursdayBreak.OpensInMinutes);

        var afterFriday = OpeningSchedule.Evaluate(text, Ist(2026, 11, 28, 1, 30)); // cumartesi 01:30
        Assert.True(afterFriday.IsOpen);
        Assert.Equal("02:00", afterFriday.ClosesAt);
    }

    [Fact]
    public void Google_english_twelve_hour_lines()
    {
        var s = OpeningSchedule.Evaluate("Monday: 9:00 AM – 10:00 PM; Tuesday: Closed", Ist(2026, 11, 23, 21, 0));
        Assert.True(s.IsOpen);
        Assert.Equal("22:00", s.ClosesAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sunrise-sunset")]
    [InlineData("by appointment")]
    public void Unknown_when_text_cannot_be_read(string? text)
    {
        var s = OpeningSchedule.Evaluate(text, DateTimeOffset.UtcNow);
        Assert.Null(s.IsOpen);
        Assert.Null(s.ClosesAt);
        Assert.Null(s.OpensAt);
    }

    [Fact]
    public void Serve_time_state_overrides_stale_cached_flag()
    {
        // Önbellekteki kayıt sabah çekilmiş ("açık"); gece hesap yeniden yapılır.
        var cached = Snapshot(openNow: true, hours: "Mo-Su 08:00-20:00");
        var (place, state) = OpenNowResolver.Apply(cached, Ist(2026, 11, 24, 22, 0));
        Assert.False(place.OpenNow);
        Assert.False(state.IsOpen);
        Assert.Equal("08:00", state.OpensAt);
    }

    [Fact]
    public void Without_hours_text_cached_flag_is_kept()
    {
        var cached = Snapshot(openNow: true, hours: null);
        var (place, state) = OpenNowResolver.Apply(cached, DateTimeOffset.UtcNow);
        Assert.True(place.OpenNow);
        Assert.True(state.IsOpen);
        Assert.Null(state.ClosesAt);
    }

    private static PlaceSnapshot Snapshot(bool? openNow, string? hours) =>
        new("osm:node:1", "Deneme", null, 40.99, 29.02, ["cafe"], openNow, null, null, null, null, [], "OSM", OpeningHoursText: hours);
}

using Nereye.Domain.Push;

namespace Nereye.Domain.Tests;

public class PushRulesTests
{
    [Theory]
    [InlineData("ExponentPushToken[xxxxxxxxxxxxxxxxxxxxxx]", true)]
    [InlineData("ExpoPushToken[abcDEF123-_]", true)]
    [InlineData("ExponentPushToken[]", false)]
    [InlineData("fcm-raw-token", false)]
    [InlineData(null, false)]
    public void Only_expo_push_tokens_are_accepted(string? token, bool ok) => Assert.Equal(ok, PushRules.IsExpoToken(token));

    private static DateTime Utc(int h, int m = 0) => new(2026, 11, 24, h, m, 0, DateTimeKind.Utc);

    [Fact]
    public void Reminder_window_is_thirty_minutes_to_three_hours_ahead()
    {
        var now = Utc(15);
        Assert.True(PushRules.ShouldRemind(Utc(16), now));
        Assert.True(PushRules.ShouldRemind(Utc(17, 59), now));
        Assert.False(PushRules.ShouldRemind(Utc(15, 20), now)); // çok yakın
        Assert.False(PushRules.ShouldRemind(Utc(18, 30), now)); // çok uzak
        Assert.False(PushRules.ShouldRemind(Utc(14), now));     // başlamış
    }

    [Fact]
    public void Daily_cap_per_device()
    {
        Assert.True(PushRules.UnderDailyCap(PushRules.DailyCap - 1));
        Assert.False(PushRules.UnderDailyCap(PushRules.DailyCap));
    }

    [Fact]
    public void Reminder_text_is_turkish_with_istanbul_time()
    {
        var (title, body) = PushRules.EventReminder("Akustik gece", "MiniMüzikhol", Utc(17, 30));
        Assert.Equal("Bu akşam: Akustik gece", title);
        Assert.Equal("MiniMüzikhol · 20:30'da başlıyor", body);
    }
}

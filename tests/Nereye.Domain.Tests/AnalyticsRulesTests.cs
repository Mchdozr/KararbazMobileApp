using Nereye.Domain.Analytics;

namespace Nereye.Domain.Tests;

public class AnalyticsRulesTests
{
    [Theory]
    [InlineData("venue_view", "nereye:petra", null, true, "nereye:petra")]
    [InlineData("directions", "osm:node:123456", null, true, "osm:node:123456")]
    [InlineData("list_view", null, "Kahve", true, "Kahve")]
    [InlineData("list_view", null, "Uydurma", false, null)]
    [InlineData("app_open", null, null, true, "")]
    [InlineData("venue_view", null, null, false, null)]
    [InlineData("venue_view", "has space", null, false, null)]
    [InlineData("login_email", null, null, false, null)]
    public void Only_known_events_with_valid_keys_are_counted(string name, string? placeId, string? intent, bool ok, string? key)
    {
        Assert.Equal(ok, AnalyticsRules.TryKey(name, placeId, intent, out var k));
        if (ok)
        {
            Assert.Equal(key, k);
        }
    }

    [Fact]
    public void Funnel_conversion_between_steps()
    {
        var steps = Funnel.Compute(new Dictionary<string, long>
        {
            ["app_open"] = 200,
            ["list_view"] = 150,
            ["venue_view"] = 60,
            ["directions"] = 15
        });
        Assert.Equal(["app_open", "list_view", "venue_view", "directions"], steps.Select(s => s.Name));
        Assert.Null(steps[0].FromPrevious);
        Assert.Equal(0.75, steps[1].FromPrevious);
        Assert.Equal(0.4, steps[2].FromPrevious);
        Assert.Equal(0.25, steps[3].FromPrevious);
        Assert.Equal(0.075, steps[3].FromStart);
    }

    [Fact]
    public void Funnel_handles_missing_steps()
    {
        var steps = Funnel.Compute(new Dictionary<string, long> { ["venue_view"] = 5 });
        Assert.Equal(0, steps[0].Count);
        Assert.Null(steps[1].FromPrevious);
    }
}

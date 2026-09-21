using Nereye.Domain.Places;

namespace Nereye.Domain.Tests;

public class OsmOpeningHoursTests
{
    [Fact]
    public void Twenty_four_seven_is_always_open()
    {
        var now = new DateTimeOffset(2026, 9, 21, 3, 0, 0, TimeSpan.Zero);
        Assert.True(OsmOpeningHours.IsOpen("24/7", now));
        Assert.Equal(7, OsmOpeningHours.Parse("24/7").Count);
    }

    [Fact]
    public void Weekday_range_closed_on_sunday_night()
    {
        var spec = "Mo-Fr 08:00-18:00";
        var monday = new DateTimeOffset(2026, 9, 21, 10, 0, 0, TimeSpan.Zero);
        var sunday = new DateTimeOffset(2026, 9, 20, 10, 0, 0, TimeSpan.Zero);
        Assert.Equal(DayOfWeek.Monday, monday.DayOfWeek);
        Assert.Equal(DayOfWeek.Sunday, sunday.DayOfWeek);
        Assert.True(OsmOpeningHours.IsOpen(spec, monday));
        Assert.False(OsmOpeningHours.IsOpen(spec, sunday));
    }

    [Fact]
    public void Empty_is_unknown()
    {
        Assert.Null(OsmOpeningHours.IsOpen(null, DateTimeOffset.UtcNow));
        Assert.Empty(OsmOpeningHours.Parse(" "));
    }
}

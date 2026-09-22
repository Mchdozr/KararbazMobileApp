using System.Globalization;
using Nereye.Domain.Places;

namespace Nereye.Domain.Tests;

public sealed class SearchRadiusTests
{
    [Fact]
    public void Does_not_cap_at_eight_kilometers()
    {
        Assert.Equal(25_000, SearchRadius.Clamp(25_000));
        Assert.Equal(100_000, SearchRadius.Clamp(250_000));
        Assert.Equal(200, SearchRadius.Clamp(50));
        Assert.Equal(1500, SearchRadius.Clamp(0));
    }

    [Fact]
    public void Viewbox_grows_past_eight_kilometers()
    {
        var small = NominatimViewBox.Format(41.055, 28.987, 1500).Split(',').Select(s => double.Parse(s, CultureInfo.InvariantCulture)).ToArray();
        var large = NominatimViewBox.Format(41.055, 28.987, 25_000).Split(',').Select(s => double.Parse(s, CultureInfo.InvariantCulture)).ToArray();
        Assert.True(large[2] - large[0] > small[2] - small[0]);
    }
}

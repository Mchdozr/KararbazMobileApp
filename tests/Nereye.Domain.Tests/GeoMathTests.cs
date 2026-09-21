using Nereye.Domain.Geo;

namespace Nereye.Domain.Tests;

public class GeoMathTests
{
    [Fact]
    public void Kadikoy_to_taksim_is_about_6km()
    {
        var meters = GeoMath.HaversineMeters(40.9909, 29.0303, 41.0369, 28.9850);
        Assert.InRange(meters, 5_000, 8_000);
    }

    [Fact]
    public void Same_point_is_zero()
    {
        Assert.Equal(0, GeoMath.HaversineMeters(41, 29, 41, 29), 6);
    }

    [Fact]
    public void Proximity_is_one_at_zero_meters()
    {
        Assert.Equal(1, GeoMath.Proximity01(0), 6);
    }

    [Fact]
    public void Proximity_is_zero_at_or_beyond_1500m()
    {
        Assert.Equal(0, GeoMath.Proximity01(1500), 6);
        Assert.Equal(0, GeoMath.Proximity01(8000), 6);
    }
}

using Nereye.Domain.Telemetry;

namespace Nereye.Domain.Tests;

public class PercentilesTests
{
    [Fact]
    public void Nearest_rank_percentiles()
    {
        double[] values = [15, 20, 35, 40, 50];
        Assert.Equal(15, Percentiles.Of(values, 0.05));
        Assert.Equal(35, Percentiles.Of(values, 0.5));
        Assert.Equal(50, Percentiles.Of(values, 0.95));
        Assert.Equal(50, Percentiles.Of(values, 1));
    }

    [Fact]
    public void Unsorted_input_and_empty_input()
    {
        Assert.Equal(90, Percentiles.Of([90, 10, 50, 70, 30, 20, 40, 60, 80, 100], 0.9));
        Assert.Null(Percentiles.Of([], 0.95));
    }

    [Fact]
    public void Ratio_is_null_without_samples()
    {
        Assert.Null(Percentiles.Ratio(0, 0));
        Assert.Equal(0.75, Percentiles.Ratio(3, 4));
    }
}

using Nereye.Domain.Recommendation;

namespace Nereye.Domain.Tests;

public class BayesianRatingTests
{
    [Fact]
    public void No_votes_returns_neutral_half()
    {
        Assert.Equal(0.5, BayesianRating.Normalize01(null, 0), 3);
    }

    [Fact]
    public void Few_votes_are_pulled_toward_prior()
    {
        var lowConfidence = BayesianRating.Normalize01(5, 1);
        var highConfidence = BayesianRating.Normalize01(5, 80);
        Assert.True(lowConfidence < highConfidence);
        Assert.True(lowConfidence > 0.5);
    }

    [Fact]
    public void Perfect_many_votes_approaches_one()
    {
        var value = BayesianRating.Normalize01(5, 10_000);
        Assert.True(value > 0.95);
        Assert.True(value <= 1);
    }
}

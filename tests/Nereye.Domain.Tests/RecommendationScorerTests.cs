using Nereye.Domain;
using Nereye.Domain.Recommendation;

namespace Nereye.Domain.Tests;

public class RecommendationScorerTests
{
    [Fact]
    public void Closer_venue_scores_higher_than_far_identical_peer()
    {
        var close = RecommendationScorer.Score(Base() with { DistanceMeters = 100 });
        var far = RecommendationScorer.Score(Base() with { DistanceMeters = 1400 });
        Assert.True(close > far);
    }

    [Fact]
    public void Beyond_1500m_proximity_is_zero_component()
    {
        var atEdge = RecommendationScorer.Score(Base() with { DistanceMeters = 1500 });
        var beyond = RecommendationScorer.Score(Base() with { DistanceMeters = 4000 });
        Assert.Equal(atEdge, beyond, 5);
    }

    [Fact]
    public void Closed_kultur_is_penalized_vs_open()
    {
        var open = RecommendationScorer.Score(Base() with { Intent = Intent.Kultur, OpenStatus = OpenStatus.Open });
        var closed = RecommendationScorer.Score(Base() with { Intent = Intent.Kultur, OpenStatus = OpenStatus.Closed });
        Assert.True(open - closed >= 0.25);
    }

    [Fact]
    public void Recently_visited_lowers_score()
    {
        var fresh = RecommendationScorer.Score(Base() with { RecentlyVisited = false });
        var repeat = RecommendationScorer.Score(Base() with { RecentlyVisited = true });
        Assert.True(fresh > repeat);
    }

    [Fact]
    public void Vibe_overlap_increases_score()
    {
        var none = RecommendationScorer.Score(Base() with { VibeMatch = 0 });
        var full = RecommendationScorer.Score(Base() with { VibeMatch = 1 });
        Assert.True(full > none);
    }

    private static ScoreInput Base() => new(
        DistanceMeters: 400,
        NereyeRatingAvg: 4.2,
        RatingCount: 20,
        IntentMatches: true,
        OpenStatus: OpenStatus.Open,
        VibeMatch: 0.5,
        RecentSaves: 3,
        RecentlyVisited: false,
        Intent: Intent.Kahve);
}

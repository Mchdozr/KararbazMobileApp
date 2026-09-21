using Nereye.Domain.Geo;

namespace Nereye.Domain.Recommendation;

public static class RecommendationScorer
{
    public const double DistanceWeight = 0.35;
    public const double RatingWeight = 0.20;
    public const double IntentWeight = 0.20;
    public const double OpenWeight = 0.10;
    public const double VibeWeight = 0.10;
    public const double TrendWeight = 0.05;
    public const double RepeatPenalty = 0.15;
    public const double ClosedKulturYemekPenalty = 0.25;

    public static double Score(ScoreInput input)
    {
        var openComponent = input.OpenStatus switch
        {
            OpenStatus.Open => 1.0,
            OpenStatus.Closed => 0.0,
            OpenStatus.Unknown => 0.5,
            _ => throw new ArgumentOutOfRangeException(nameof(input), input.OpenStatus, null)
        };

        var trend = Math.Clamp(input.RecentSaves / 20.0, 0, 1);

        var score =
            DistanceWeight * GeoMath.Proximity01(input.DistanceMeters)
            + RatingWeight * BayesianRating.Normalize01(input.NereyeRatingAvg, input.RatingCount)
            + IntentWeight * (input.IntentMatches ? 1 : 0)
            + OpenWeight * openComponent
            + VibeWeight * Math.Clamp(input.VibeMatch, 0, 1)
            + TrendWeight * trend
            - RepeatPenalty * (input.RecentlyVisited ? 1 : 0);

        if (input.OpenStatus == OpenStatus.Closed &&
            (input.Intent is Intent.Kultur or Intent.Yemek))
        {
            score -= ClosedKulturYemekPenalty;
        }

        return score;
    }
}

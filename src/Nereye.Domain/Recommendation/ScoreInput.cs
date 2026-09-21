namespace Nereye.Domain.Recommendation;

public sealed record ScoreInput(
    double DistanceMeters,
    double? NereyeRatingAvg,
    int RatingCount,
    bool IntentMatches,
    OpenStatus OpenStatus,
    double VibeMatch,
    int RecentSaves,
    bool RecentlyVisited,
    Intent Intent);

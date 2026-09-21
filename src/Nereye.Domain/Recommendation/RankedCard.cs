namespace Nereye.Domain.Recommendation;

public enum CardKind
{
    Organic,
    Sponsored
}

public sealed record RankedCard(string PlaceId, double Score, CardKind Kind);

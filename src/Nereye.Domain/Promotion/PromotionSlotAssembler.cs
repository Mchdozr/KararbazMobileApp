using Nereye.Domain.Recommendation;

namespace Nereye.Domain.Promotion;

public static class PromotionSlotAssembler
{
    public const int MaxSponsored = 2;
    public const int MinOrganic = 5;
    public const int MaxOrganic = 60;

    public static IReadOnlyList<RankedCard> Assemble(
        IReadOnlyList<RankedCard> organic,
        IReadOnlyList<RankedCard> sponsored)
    {
        var organicIds = organic.Select(c => c.PlaceId).ToHashSet(StringComparer.Ordinal);
        var uniqueSponsored = sponsored
            .Where(c => !organicIds.Contains(c.PlaceId))
            .Take(MaxSponsored)
            .Select(c => c with { Kind = CardKind.Sponsored })
            .ToList();

        var keptOrganic = organic
            .Take(MaxOrganic)
            .Select(c => c with { Kind = CardKind.Organic })
            .ToList();

        if (keptOrganic.Count == 0)
        {
            return uniqueSponsored;
        }

        var result = new List<RankedCard>(keptOrganic.Count + uniqueSponsored.Count);
        result.AddRange(uniqueSponsored);
        result.AddRange(keptOrganic);
        return result;
    }
}

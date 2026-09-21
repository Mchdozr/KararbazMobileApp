using Nereye.Domain.Promotion;
using Nereye.Domain.Recommendation;

namespace Nereye.Domain.Tests;

public class PromotionSlotAssemblerTests
{
    [Fact]
    public void Injects_at_most_two_sponsored_and_keeps_five_organic()
    {
        var organic = Enumerable.Range(1, 8)
            .Select(i => new RankedCard($"o{i}", 1 - i * 0.01, CardKind.Organic))
            .ToList();
        var sponsored = Enumerable.Range(1, 5)
            .Select(i => new RankedCard($"s{i}", 0.9, CardKind.Sponsored))
            .ToList();

        var result = PromotionSlotAssembler.Assemble(organic, sponsored);

        Assert.Equal(2, result.Count(c => c.Kind == CardKind.Sponsored));
        Assert.Equal(8, result.Count(c => c.Kind == CardKind.Organic));
        Assert.Equal(CardKind.Sponsored, result[0].Kind);
        Assert.All(result.Where(c => c.Kind == CardKind.Sponsored), c => Assert.StartsWith("s", c.PlaceId));
    }

    [Fact]
    public void Does_not_duplicate_a_place_already_in_organic()
    {
        var organic = new[] { new RankedCard("same", 0.9, CardKind.Organic) }
            .Concat(Enumerable.Range(2, 6).Select(i => new RankedCard($"o{i}", 0.8, CardKind.Organic)))
            .ToList();
        var sponsored = new[] { new RankedCard("same", 0.99, CardKind.Sponsored) };

        var result = PromotionSlotAssembler.Assemble(organic, sponsored);

        Assert.Equal(1, result.Count(c => c.PlaceId == "same"));
        Assert.Equal(CardKind.Organic, result.Single(c => c.PlaceId == "same").Kind);
    }
}

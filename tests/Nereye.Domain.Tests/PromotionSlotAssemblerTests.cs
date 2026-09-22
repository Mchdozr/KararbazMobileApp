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
    public void Keeps_more_than_eight_organic()
    {
        var organic = Enumerable.Range(1, 24)
            .Select(i => new RankedCard($"o{i}", 1 - i * 0.01, CardKind.Organic))
            .ToList();

        var result = PromotionSlotAssembler.Assemble(organic, []);

        Assert.Equal(24, result.Count(c => c.Kind == CardKind.Organic));
    }

    [Fact]
    public void Page_slice_exposes_has_more()
    {
        var items = Enumerable.Range(1, 25).ToList();
        var (page, more) = RecommendationPage.Slice(items, 0, 20);
        Assert.Equal(20, page.Count);
        Assert.True(more);
        var (last, lastMore) = RecommendationPage.Slice(items, 20, 20);
        Assert.Equal(5, last.Count);
        Assert.False(lastMore);
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

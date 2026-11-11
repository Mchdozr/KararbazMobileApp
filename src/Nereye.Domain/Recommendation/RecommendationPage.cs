namespace Nereye.Domain.Recommendation;

public static class RecommendationPage
{
    public const int DefaultTake = 20;
    public const int MaxTake = 120;

    public static (IReadOnlyList<T> Items, bool HasMore) Slice<T>(IReadOnlyList<T> items, int skip, int take)
    {
        var start = Math.Max(0, skip);
        var size = take <= 0 ? DefaultTake : Math.Clamp(take, 1, MaxTake);
        var page = items.Skip(start).Take(size).ToList();
        return (page, start + page.Count < items.Count);
    }
}

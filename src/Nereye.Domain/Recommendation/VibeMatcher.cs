namespace Nereye.Domain.Recommendation;

public static class VibeMatcher
{
    public static double Score(IReadOnlyCollection<VibeTag> userVibes, IReadOnlyCollection<VibeTag> venueVibes)
    {
        if (userVibes.Count == 0)
        {
            return 0.5;
        }

        var overlap = userVibes.Intersect(venueVibes).Count();
        return (double)overlap / userVibes.Count;
    }
}

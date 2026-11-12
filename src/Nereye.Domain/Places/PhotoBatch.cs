namespace Nereye.Domain.Places;

public static class PhotoBatch
{
    public const int MaxPlaces = 8;

    public static IReadOnlyList<string> Take(IEnumerable<string> placeIds)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var ids = new List<string>(MaxPlaces);
        foreach (var raw in placeIds)
        {
            var id = raw?.Trim();
            if (string.IsNullOrWhiteSpace(id) || !seen.Add(id))
            {
                continue;
            }

            ids.Add(id);
            if (ids.Count == MaxPlaces)
            {
                break;
            }
        }

        return ids;
    }
}

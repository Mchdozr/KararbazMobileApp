using System.Text.Json;

namespace Nereye.Domain.Places;

public static class MapillaryPhoto
{
    public static string? Parse(JsonElement json)
    {
        if (!json.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var row in data.EnumerateArray())
        {
            if (row.TryGetProperty("thumb_1024_url", out var thumb) && thumb.ValueKind == JsonValueKind.String)
            {
                var url = thumb.GetString();
                if (PlacePhoto.IsHttp(url))
                {
                    return url;
                }
            }
        }

        return null;
    }
}

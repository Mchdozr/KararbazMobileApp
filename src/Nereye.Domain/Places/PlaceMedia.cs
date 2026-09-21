namespace Nereye.Domain.Places;

public static class PlaceMedia
{
    public static string? Url(string? overlayPhoto, string? photoReference)
    {
        if (!string.IsNullOrWhiteSpace(overlayPhoto))
        {
            return overlayPhoto;
        }

        if (!string.IsNullOrWhiteSpace(photoReference)
            && (photoReference.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || photoReference.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || photoReference.StartsWith("/media/", StringComparison.OrdinalIgnoreCase)
                || photoReference.StartsWith("/api/media/", StringComparison.OrdinalIgnoreCase)))
        {
            return photoReference;
        }

        if (!string.IsNullOrWhiteSpace(photoReference)
            && photoReference.StartsWith("places/", StringComparison.OrdinalIgnoreCase))
        {
            return "/api/media/google?name=" + Uri.EscapeDataString(photoReference);
        }

        return null;
    }
}

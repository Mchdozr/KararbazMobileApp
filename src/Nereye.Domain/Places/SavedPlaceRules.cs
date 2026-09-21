namespace Nereye.Domain.Places;

public static class SavedPlaceRules
{
    public const int MaxPerUser = 20;

    public static bool CanAdd(int currentCount) => currentCount < MaxPerUser;

    public static string NormalizeKind(string? kind)
    {
        var k = (kind ?? "").Trim().ToLowerInvariant();
        return k switch
        {
            "home" or "ev" => "home",
            "work" or "is" or "iş" => "work",
            "custom" or "pin" or "kayit" or "kayıt" => "custom",
            _ => "custom"
        };
    }

    public static string NormalizeLabel(string? label, string kind)
    {
        var t = (label ?? "").Trim();
        if (t.Length >= 2)
        {
            return t.Length > 40 ? t[..40] : t;
        }

        return kind switch
        {
            "home" => "Ev",
            "work" => "İş",
            _ => "Kayıt"
        };
    }

    public static bool ValidCoord(double latitude, double longitude) =>
        latitude is >= -90 and <= 90
        && longitude is >= -180 and <= 180
        && (Math.Abs(latitude) > 0.0001 || Math.Abs(longitude) > 0.0001);
}

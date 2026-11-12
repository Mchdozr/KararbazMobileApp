namespace Nereye.Domain.Places;

public static class PlacesCacheTtl
{
    public static readonly TimeSpan Nearby = TimeSpan.FromHours(6);
    public static readonly TimeSpan Empty = TimeSpan.FromMinutes(2);
}

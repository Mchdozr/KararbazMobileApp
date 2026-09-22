namespace Nereye.Domain.Places;

public static class SearchRadius
{
    public const int DefaultMeters = 1500;
    public const int MinMeters = 200;
    public const int MaxMeters = 100_000;

    public static int Clamp(int meters) =>
        meters <= 0 ? DefaultMeters : Math.Clamp(meters, MinMeters, MaxMeters);
}

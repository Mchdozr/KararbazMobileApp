namespace Nereye.Domain.Telemetry;

/// <summary>Gecikme özetleri için en yakın sıra (nearest-rank) yüzdeliği.</summary>
public static class Percentiles
{
    public static double? Of(IReadOnlyCollection<double> values, double p)
    {
        if (values.Count == 0)
        {
            return null;
        }

        var sorted = values.Order().ToArray();
        var rank = (int)Math.Ceiling(Math.Clamp(p, 0, 1) * sorted.Length);
        return sorted[Math.Clamp(rank, 1, sorted.Length) - 1];
    }

    public static double? Ratio(long part, long total) => total <= 0 ? null : (double)part / total;
}

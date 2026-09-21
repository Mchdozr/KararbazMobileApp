namespace Nereye.Domain.Recommendation;

public static class BayesianRating
{
    public const double PriorMean = 3.5;
    public const int PriorWeight = 8;

    public static double Normalize01(double? average, int count)
    {
        if (count <= 0 || average is null)
        {
            return 0.5;
        }

        var bayesian = ((PriorWeight * PriorMean) + (count * average.Value)) / (PriorWeight + count);
        return Math.Clamp((bayesian - 1) / 4.0, 0, 1);
    }
}

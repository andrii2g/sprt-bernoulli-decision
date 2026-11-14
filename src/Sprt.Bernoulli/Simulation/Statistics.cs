namespace Sprt.Bernoulli.Simulation;

public sealed record RateEstimate(int Events, int Trials, double Rate, double Lower95, double Upper95)
{
    public static RateEstimate Wilson(int events, int trials)
    {
        if (trials < 1 || events < 0 || events > trials)
            throw new ArgumentOutOfRangeException(nameof(events));
        const double z = 1.959963984540054;
        double rate = (double)events / trials;
        double denominator = 1 + z * z / trials;
        double center = (rate + z * z / (2 * trials)) / denominator;
        double margin = z * Math.Sqrt(rate * (1 - rate) / trials + z * z / (4.0 * trials * trials)) / denominator;
        return new(events, trials, rate, Math.Max(0, center - margin), Math.Min(1, center + margin));
    }
}

public sealed record SampleSummary(double Mean, double Median, int P95, int Minimum, int Maximum)
{
    public static SampleSummary From(int[] samples)
    {
        if (samples.Length == 0)
            throw new ArgumentException("Samples must not be empty.", nameof(samples));
        int[] sorted = samples.Order().ToArray();
        int middle = sorted.Length / 2;
        double median = sorted.Length % 2 == 0
            ? ((double)sorted[middle - 1] + sorted[middle]) / 2 : sorted[middle];
        return new(samples.Average(), median, sorted[(int)Math.Ceiling(0.95 * sorted.Length) - 1], sorted[0], sorted[^1]);
    }
}

public static class Binomial
{
    /// <summary>Normalized probabilities computed outwards from the mode to avoid factorials and overflow.</summary>
    public static double[] Probabilities(int n, double p)
    {
        if (n < 0 || !double.IsFinite(p) || p < 0 || p > 1)
            throw new ArgumentOutOfRangeException(nameof(p));
        var weights = new double[n + 1];
        if (p == 0) { weights[0] = 1; return weights; }
        if (p == 1) { weights[n] = 1; return weights; }
        int mode = Math.Min(n, (int)((n + 1.0) * p));
        weights[mode] = 1;
        for (int k = mode; k > 0; k--)
            weights[k - 1] = weights[k] * (k * (1 - p)) / ((n - k + 1) * p);
        for (int k = mode; k < n; k++)
            weights[k + 1] = weights[k] * ((n - k) * p) / ((k + 1) * (1 - p));
        double sum = weights.Sum();
        for (int k = 0; k <= n; k++) weights[k] /= sum;
        return weights;
    }
}

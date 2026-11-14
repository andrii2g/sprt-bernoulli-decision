namespace Sprt.Bernoulli.Simulation;

/// <summary>A binary fixed-sample LRT, using success counts or failure counts in the H1 direction.</summary>
public sealed record FixedSampleRule(int Samples, int MinimumEvidenceCount, bool CountSuccesses)
{
    public bool FavorsH1(int successes)
    {
        if (successes < 0 || successes > Samples)
            throw new ArgumentOutOfRangeException(nameof(successes));
        return (CountSuccesses ? successes : Samples - successes) >= MinimumEvidenceCount;
    }
}

public sealed record CalibrationResult(
    FixedSampleRule Rule,
    RateEstimate CalibrationFalsePositive,
    RateEstimate CalibrationFalseNegative,
    double ExactFalsePositive,
    double ExactFalseNegative,
    bool ExactTargetsMet);

public static class FixedSampleCalibrator
{
    /// <summary>
    /// Searches N in ascending order using nested simulated Bernoulli prefixes.
    /// At each N, chooses the feasible cutoff minimizing the larger relative error.
    /// Selection uses point estimates; the exact binomial check is reported separately.
    /// </summary>
    public static CalibrationResult Calibrate(SprtOptions options, int trials, int maxSamples, ulong seed)
    {
        options.Validate();
        if (trials < 1 || maxSamples < 1)
            throw new ArgumentOutOfRangeException(nameof(trials));
        bool ascending = options.P1 > options.P0;
        double q0 = ascending ? options.P0 : 1 - options.P0;
        double q1 = ascending ? options.P1 : 1 - options.P1;
        var random0 = new DeterministicRandom(DeterministicRandom.DeriveSeed(seed, 0));
        var random1 = new DeterministicRandom(DeterministicRandom.DeriveSeed(seed, 1));
        var counts0 = new int[trials];
        var counts1 = new int[trials];

        for (int n = 1; n <= maxSamples; n++)
        {
            var histogram0 = new int[n + 1];
            var histogram1 = new int[n + 1];
            for (int trial = 0; trial < trials; trial++)
            {
                if (random0.NextDouble() < q0) counts0[trial]++;
                if (random1.NextDouble() < q1) counts1[trial]++;
                histogram0[counts0[trial]]++;
                histogram1[counts1[trial]]++;
            }

            int fp = trials, fn = 0, bestK = -1, bestFp = 0, bestFn = 0;
            double bestScore = double.PositiveInfinity;
            for (int k = 0; k <= n + 1; k++)
            {
                double score = Math.Max(fp / (trials * options.Alpha), fn / (trials * options.Beta));
                if (score <= 1 && score < bestScore)
                {
                    (bestK, bestFp, bestFn, bestScore) = (k, fp, fn, score);
                }
                if (k <= n) { fp -= histogram0[k]; fn += histogram1[k]; }
            }

            if (bestK >= 0)
            {
                double exactFp = Binomial.Probabilities(n, q0).Skip(bestK).Sum();
                double exactFn = Binomial.Probabilities(n, q1).Take(bestK).Sum();
                return new(new(n, bestK, ascending), RateEstimate.Wilson(bestFp, trials),
                    RateEstimate.Wilson(bestFn, trials), exactFp, exactFn,
                    exactFp <= options.Alpha && exactFn <= options.Beta);
            }
        }

        throw new InvalidOperationException(
            $"No fixed-sample rule met the simulated targets through N={maxSamples}. Increase --max-fixed-samples or relax the targets.");
    }
}

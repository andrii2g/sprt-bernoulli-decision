namespace Sprt.Bernoulli.Simulation;

public sealed record SimulationOptions(
    SprtOptions Test,
    int Trials = 10000,
    int CalibrationTrials = 10000,
    int MaxFixedSamples = 1000,
    ulong Seed = 42,
    int TrajectoriesPerScenario = 6)
{
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Test);
        Test.Validate();
        if (Trials < 1 || CalibrationTrials < 1 || MaxFixedSamples < 1)
            throw new ArgumentOutOfRangeException(nameof(Trials), "Trial counts and the baseline search limit must be positive.");
        if (TrajectoriesPerScenario < 0 || TrajectoriesPerScenario > Trials)
            throw new ArgumentOutOfRangeException(nameof(TrajectoriesPerScenario));
    }
}

public sealed record Trajectory(int Trial, SprtStep[] Steps);
public sealed record SampleFrequency(int Samples, int Trials);
public sealed record MethodSummary(
    SampleSummary Samples,
    RateEstimate FavorH0,
    RateEstimate FavorH1,
    RateEstimate Inconclusive,
    RateEstimate? FalsePositive,
    RateEstimate? FalseNegative);
public sealed record ScenarioResult(
    string Name,
    double TrueProbability,
    MethodSummary Sequential,
    MethodSummary FixedSample,
    double SampleSavingsFraction,
    SampleFrequency[] SampleDistribution,
    Trajectory[] Trajectories);
public sealed record ExperimentResult(
    string FormatVersion,
    string Generator,
    SimulationOptions Options,
    ulong CalibrationSeed,
    ulong EvaluationSeed,
    double LowerBoundary,
    double UpperBoundary,
    CalibrationResult Calibration,
    ScenarioResult[] Scenarios);

public static class SimulationRunner
{
    public static ExperimentResult Run(SimulationOptions options)
    {
        options.Validate();
        ulong calibrationSeed = DeterministicRandom.DeriveSeed(options.Seed, 0);
        ulong evaluationSeed = DeterministicRandom.DeriveSeed(options.Seed, 1);
        var calibration = FixedSampleCalibrator.Calibrate(options.Test, options.CalibrationTrials,
            options.MaxFixedSamples, calibrationSeed);
        var boundaries = new BernoulliSprt(options.Test);
        double midpoint = options.Test.P0 + (options.Test.P1 - options.Test.P0) / 2;
        ScenarioResult[] scenarios =
        [
            Evaluate("H0", options.Test.P0, 0, options, calibration.Rule, evaluationSeed),
            Evaluate("Midpoint", midpoint, 1, options, calibration.Rule, evaluationSeed),
            Evaluate("H1", options.Test.P1, 2, options, calibration.Rule, evaluationSeed)
        ];
        return new("1", "SplitMix64 / upper 53 bits / Bernoulli u < p", options,
            calibrationSeed, evaluationSeed, boundaries.LowerBoundary, boundaries.UpperBoundary, calibration, scenarios);
    }

    private static ScenarioResult Evaluate(string name, double p, ulong scenario,
        SimulationOptions options, FixedSampleRule rule, ulong evaluationSeed)
    {
        int h0 = 0, h1 = 0, inconclusive = 0, fixedH1 = 0;
        var sampleCounts = new int[options.Trials];
        var trajectories = new List<Trajectory>();
        ulong scenarioSeed = DeterministicRandom.DeriveSeed(evaluationSeed, scenario);
        for (int trial = 0; trial < options.Trials; trial++)
        {
            var random = new DeterministicRandom(DeterministicRandom.DeriveSeed(scenarioSeed, (ulong)trial));
            var test = new BernoulliSprt(options.Test);
            List<SprtStep>? steps = trial < options.TrajectoriesPerScenario ? [test.Current] : null;
            int successes = 0;
            int n = 0;
            // Both methods see the same prefix; continue generating only until both have finished.
            while (n < rule.Samples || test.Current.Decision == SprtDecision.Continue)
            {
                bool success = random.NextDouble() < p;
                n++;
                if (n <= rule.Samples && success) successes++;
                if (test.Current.Decision == SprtDecision.Continue)
                {
                    test.Observe(success);
                    steps?.Add(test.Current);
                }
            }
            switch (test.Current.Decision)
            {
                case SprtDecision.FavorH0: h0++; break;
                case SprtDecision.FavorH1: h1++; break;
                case SprtDecision.Inconclusive: inconclusive++; break;
                default: throw new InvalidOperationException("Simulation ended before its decision.");
            }
            if (rule.FavorsH1(successes)) fixedH1++;
            sampleCounts[trial] = test.Current.Samples;
            if (steps is not null) trajectories.Add(new(trial, steps.ToArray()));
        }

        MethodSummary Summarize(SampleSummary samples, int favor0, int favor1, int unresolved) =>
            new(samples, RateEstimate.Wilson(favor0, options.Trials), RateEstimate.Wilson(favor1, options.Trials),
                RateEstimate.Wilson(unresolved, options.Trials),
                name == "H0" ? RateEstimate.Wilson(favor1, options.Trials) : null,
                name == "H1" ? RateEstimate.Wilson(favor0, options.Trials) : null);

        var sequential = Summarize(SampleSummary.From(sampleCounts), h0, h1, inconclusive);
        var fixedSample = Summarize(new(rule.Samples, rule.Samples, rule.Samples, rule.Samples, rule.Samples),
            options.Trials - fixedH1, fixedH1, 0);
        return new(name, p, sequential, fixedSample, 1 - sequential.Samples.Mean / rule.Samples,
            sampleCounts.GroupBy(n => n).OrderBy(g => g.Key).Select(g => new SampleFrequency(g.Key, g.Count())).ToArray(),
            trajectories.ToArray());
    }
}

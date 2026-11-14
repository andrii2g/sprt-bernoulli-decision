using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sprt.Bernoulli;
using Sprt.Bernoulli.Simulation;

return Run(args);

static int Run(string[] args)
{
    if (args.Length == 0 || args is ["--help"] or ["-h"] or ["help"])
    {
        Console.WriteLine("""
            Bernoulli SPRT · .NET 10

            simulate [options]   Calibrate a fixed test and evaluate H0, midpoint, H1.
            trace --observations 101101 [options]   Follow an explicit observation stream.

            Shared options:
              --p0 0.4 --p1 0.6 --alpha 0.05 --beta 0.05 --max-samples 500
            Simulation options:
              --trials 10000 --calibration-trials 10000 --max-fixed-samples 1000
              --seed 42 --trajectories 6 --output reports/latest

            Values use invariant culture (decimal point). All options take a value.
            Error targets are nominal. A cutoff without a crossing is inconclusive.
            """);
        return 0;
    }

    try
    {
        string command = args[0];
        if (command is not ("simulate" or "trace"))
            throw new ArgumentException($"Unknown command '{command}'. Use --help.");
        string[] shared = ["p0", "p1", "alpha", "beta", "max-samples"];
        string[] extra = command == "trace" ? ["observations"]
            : ["trials", "calibration-trials", "max-fixed-samples", "seed", "trajectories", "output"];
        var allowed = shared.Concat(extra).ToHashSet(StringComparer.Ordinal);
        var flags = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 1; i < args.Length; i += 2)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal) || !allowed.Contains(args[i][2..]))
                throw new ArgumentException($"Unknown option '{args[i]}'. Use --help.");
            if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Option '{args[i]}' needs a value.");
            if (!flags.TryAdd(args[i][2..], args[i + 1]))
                throw new ArgumentException($"Duplicate option '{args[i]}'.");
        }

        string Get(string key, string fallback) => flags.GetValueOrDefault(key, fallback);
        double Number(string key, string fallback) => double.Parse(Get(key, fallback), CultureInfo.InvariantCulture);
        int Integer(string key, string fallback) => int.Parse(Get(key, fallback), CultureInfo.InvariantCulture);
        var testOptions = new SprtOptions(Number("p0", "0.4"), Number("p1", "0.6"),
            Number("alpha", "0.05"), Number("beta", "0.05"), Integer("max-samples", "500"));
        testOptions.Validate();
        if (command == "trace")
        {
            string observations = Get("observations", "");
            if (observations.Length == 0 || observations.Any(c => c is not ('0' or '1')))
                throw new ArgumentException("--observations must be a nonempty string containing only 0 and 1.");
            var test = new BernoulliSprt(testOptions);
            Console.WriteLine(FormattableString.Invariant($"Boundaries: {test.LowerBoundary:F6}, {test.UpperBoundary:F6}"));
            Console.WriteLine("sample,successes,logLikelihoodRatio,decision");
            Console.WriteLine("0,0,0,Continue");
            foreach (char observation in observations)
            {
                SprtStep step = test.Observe(observation == '1');
                Console.WriteLine(FormattableString.Invariant($"{step.Samples},{step.Successes},{step.LogLikelihoodRatio:G17},{step.Decision}"));
                if (step.Decision != SprtDecision.Continue) break;
            }
            int unused = observations.Length - test.Current.Samples;
            if (unused > 0) Console.WriteLine($"Stopped; ignored {unused} remaining observations.");
            if (test.Current.Decision == SprtDecision.Continue)
                Console.WriteLine("Stream exhausted before a boundary or the sample cutoff; decision is Continue.");
            return 0;
        }

        int trials = Integer("trials", "10000");
        var options = new SimulationOptions(testOptions, trials, Integer("calibration-trials", "10000"),
            Integer("max-fixed-samples", "1000"), ulong.Parse(Get("seed", "42"), CultureInfo.InvariantCulture),
            Integer("trajectories", Math.Min(6, trials).ToString(CultureInfo.InvariantCulture)));
        options.Validate();
        string output = Path.GetFullPath(Get("output", "reports/latest"));
        Console.WriteLine($"Calibrating baseline and evaluating {trials:N0} trials per scenario...");
        ExperimentResult result = SimulationRunner.Run(options);
        Directory.CreateDirectory(output);
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        jsonOptions.Converters.Add(new JsonStringEnumConverter());
        File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(result, jsonOptions));

        FixedSampleRule rule = result.Calibration.Rule;
        Console.WriteLine($"Fixed rule: N={rule.Samples}, {(rule.CountSuccesses ? "successes" : "failures")} >= {rule.MinimumEvidenceCount} favors H1.");
        Console.WriteLine(FormattableString.Invariant($"Exact baseline errors: FP={result.Calibration.ExactFalsePositive:P3}, FN={result.Calibration.ExactFalseNegative:P3}; targets met: {result.Calibration.ExactTargetsMet}."));
        Console.WriteLine("Scenario    SPRT mean    Median    P95    Inconclusive    Error (all trials)    Sample savings");
        foreach (var scenario in result.Scenarios)
        {
            var s = scenario.Sequential;
            RateEstimate? error = s.FalsePositive ?? s.FalseNegative;
            string errorText = error is null ? "n/a" : error.Rate.ToString("P2", CultureInfo.InvariantCulture);
            Console.WriteLine(FormattableString.Invariant($"{scenario.Name,-10} {s.Samples.Mean,9:F2} {s.Samples.Median,9:F1} {s.Samples.P95,6} {s.Inconclusive.Rate,15:P2} {errorText,21} {scenario.SampleSavingsFraction,17:P2}"));
        }
        Console.WriteLine($"Results: {Path.Combine(output, "results.json")}");
        return 0;
    }
    catch (Exception error) when (error is ArgumentException or FormatException or OverflowException
        or InvalidOperationException or IOException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine($"Error: {error.Message}");
        return 1;
    }
}

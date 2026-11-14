namespace Sprt.Bernoulli;

public enum SprtDecision
{
    Continue,
    FavorH0,
    FavorH1,
    Inconclusive
}

public readonly record struct SprtStep(
    int Samples,
    int Successes,
    double LogLikelihoodRatio,
    SprtDecision Decision);

/// <summary>
/// Stateful, single-stream Wald SPRT. Not thread safe. Boundary checks include
/// equality and precede the sample cutoff. Wald targets are approximate.
/// </summary>
public sealed class BernoulliSprt
{
    private readonly SprtOptions options;
    private readonly double successIncrement;
    private readonly double failureIncrement;
    private double compensation;

    public BernoulliSprt(SprtOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        this.options = options;
        // Differences of logs avoid overflow in probability ratios.
        successIncrement = Math.Log(options.P1) - Math.Log(options.P0);
        failureIncrement = Math.Log(1 - options.P1) - Math.Log(1 - options.P0);
        LowerBoundary = Math.Log(options.Beta) - Math.Log(1 - options.Alpha);
        UpperBoundary = Math.Log(1 - options.Beta) - Math.Log(options.Alpha);
    }

    public double LowerBoundary { get; }
    public double UpperBoundary { get; }
    public SprtStep Current { get; private set; }

    public SprtStep Observe(bool success)
    {
        if (Current.Decision != SprtDecision.Continue)
            throw new InvalidOperationException("This test has stopped. Create a new test for another stream.");

        // Compensated summation limits drift along long trajectories.
        double adjusted = (success ? successIncrement : failureIncrement) - compensation;
        double evidence = Current.LogLikelihoodRatio + adjusted;
        compensation = (evidence - Current.LogLikelihoodRatio) - adjusted;
        int samples = Current.Samples + 1;
        SprtDecision decision = evidence >= UpperBoundary ? SprtDecision.FavorH1
            : evidence <= LowerBoundary ? SprtDecision.FavorH0
            : samples == options.MaxSamples ? SprtDecision.Inconclusive
            : SprtDecision.Continue;

        Current = new(samples, Current.Successes + (success ? 1 : 0), evidence, decision);
        return Current;
    }
}

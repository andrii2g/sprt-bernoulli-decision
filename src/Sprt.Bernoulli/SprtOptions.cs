namespace Sprt.Bernoulli;

/// <summary>Two simple iid Bernoulli hypotheses and nominal Wald error targets.</summary>
public sealed record SprtOptions(
    double P0,
    double P1,
    double Alpha = 0.05,
    double Beta = 0.05,
    int MaxSamples = 500)
{
    public void Validate()
    {
        RequireProbability(P0, nameof(P0));
        RequireProbability(P1, nameof(P1));
        RequireProbability(Alpha, nameof(Alpha));
        RequireProbability(Beta, nameof(Beta));
        if (P0 == P1)
            throw new ArgumentException("The hypotheses must have different probabilities.");
        if (Alpha + Beta >= 1)
            throw new ArgumentException("Alpha + Beta must be less than one.");
        if (MaxSamples < 1)
            throw new ArgumentOutOfRangeException(nameof(MaxSamples), "The sample limit must be positive.");
    }

    private static void RequireProbability(double value, string name)
    {
        if (!double.IsFinite(value) || value <= 0 || value >= 1)
            throw new ArgumentOutOfRangeException(name, "Must be finite and strictly between zero and one.");
    }
}

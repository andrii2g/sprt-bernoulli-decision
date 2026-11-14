using Sprt.Bernoulli;

namespace Sprt.Bernoulli.Tests;

public sealed class BernoulliSprtTests
{
    [Theory]
    [InlineData(true, SprtDecision.FavorH1)]
    [InlineData(false, SprtDecision.FavorH0)]
    public void EqualityAtBoundaryWinsOverCutoff(bool observation, SprtDecision expected)
    {
        var test = new BernoulliSprt(new(0.25, 0.75, 0.25, 0.25, 1));
        Assert.Equal(expected, test.Observe(observation).Decision);
        Assert.Throws<InvalidOperationException>(() => test.Observe(observation));
        Assert.Equal(1, test.Current.Samples);
    }

    [Theory]
    [InlineData(0.4, 0.6, true, SprtDecision.FavorH1)]
    [InlineData(0.4, 0.6, false, SprtDecision.FavorH0)]
    [InlineData(0.6, 0.4, true, SprtDecision.FavorH0)]
    [InlineData(0.6, 0.4, false, SprtDecision.FavorH1)]
    public void RepeatedEvidenceStopsAtFirstCrossing(double p0, double p1, bool value, SprtDecision expected)
    {
        var test = new BernoulliSprt(new(p0, p1));
        while (test.Current.Decision == SprtDecision.Continue)
        {
            Assert.InRange(test.Current.LogLikelihoodRatio, test.LowerBoundary, test.UpperBoundary);
            test.Observe(value);
        }
        Assert.Equal(8, test.Current.Samples);
        Assert.Equal(expected, test.Current.Decision);
    }

    [Fact]
    public void CumulativeEvidenceMatchesCountFormula()
    {
        var test = new BernoulliSprt(new(0.4, 0.6, MaxSamples: 20));
        bool[] observations = [true, false, true, true, false, false, true];
        foreach (bool value in observations)
            test.Observe(value);
        double expected = 4 * Math.Log(0.6 / 0.4) + 3 * Math.Log(0.4 / 0.6);
        Assert.Equal(expected, test.Current.LogLikelihoodRatio, 12);
        Assert.Equal(4, test.Current.Successes);
        Assert.Equal(SprtDecision.Continue, test.Current.Decision);
    }

    [Fact]
    public void CutoffIsInconclusiveAndTerminal()
    {
        var test = new BernoulliSprt(new(0.4, 0.6, MaxSamples: 2));
        test.Observe(true);
        Assert.Equal(SprtDecision.Inconclusive, test.Observe(false).Decision);
        Assert.Throws<InvalidOperationException>(() => test.Observe(true));
    }

    [Fact]
    public void ExtremeValidProbabilitiesProduceFiniteEvidence()
    {
        var test = new BernoulliSprt(new(double.Epsilon, 0.5));
        Assert.True(double.IsFinite(test.Observe(true).LogLikelihoodRatio));
    }

    [Theory]
    [InlineData(0, 0.6, 0.05, 0.05, 10)]
    [InlineData(0.4, 1, 0.05, 0.05, 10)]
    [InlineData(double.NaN, 0.6, 0.05, 0.05, 10)]
    [InlineData(0.4, double.PositiveInfinity, 0.05, 0.05, 10)]
    [InlineData(0.4, 0.4, 0.05, 0.05, 10)]
    [InlineData(0.4, 0.6, 0, 0.05, 10)]
    [InlineData(0.4, 0.6, 0.05, double.NaN, 10)]
    [InlineData(0.4, 0.6, 0.5, 0.5, 10)]
    [InlineData(0.4, 0.6, 0.05, 0.05, 0)]
    public void InvalidOptionsAreRejected(double p0, double p1, double alpha, double beta, int max)
    {
        Assert.ThrowsAny<ArgumentException>(() => new BernoulliSprt(new(p0, p1, alpha, beta, max)));
    }
}

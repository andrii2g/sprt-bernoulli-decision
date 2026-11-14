using System.Text.Json;
using Sprt.Bernoulli.Simulation;

namespace Sprt.Bernoulli.Tests;

public sealed class SimulationTests
{
    [Fact]
    public void SplitMix64MatchesPublishedSequence()
    {
        var random = new DeterministicRandom(0);
        Assert.Equal(0xE220A8397B1DCDAFUL, random.NextUInt64());
        Assert.Equal(0x6E789E6AA1B965F4UL, random.NextUInt64());
    }

    [Fact]
    public void BinomialMatchesKnownProbabilitiesAndHandlesEndpoints()
    {
        double[] pmf = Binomial.Probabilities(2, 0.2);
        Assert.Equal(0.64, pmf[0], 12);
        Assert.Equal(0.32, pmf[1], 12);
        Assert.Equal(0.04, pmf[2], 12);
        Assert.Equal([1.0, 0, 0], Binomial.Probabilities(2, 0));
        Assert.Equal([0.0, 0, 1], Binomial.Probabilities(2, 1));
        double[] large = Binomial.Probabilities(2000, 0.99);
        Assert.Equal(1, large.Sum(), 12);
        Assert.All(large, p => Assert.True(double.IsFinite(p) && p >= 0));
    }

    [Theory]
    [InlineData(0, 1000, 0, 0.00382676)]
    [InlineData(50, 100, 0.4038315, 0.5961685)]
    [InlineData(1000, 1000, 0.9961732, 1)]
    public void WilsonIntervalsMatchKnownValues(int events, int trials, double lower, double upper)
    {
        var interval = RateEstimate.Wilson(events, trials);
        Assert.Equal(lower, interval.Lower95, 6);
        Assert.Equal(upper, interval.Upper95, 6);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FixedRuleUsesEvidenceInEitherDirection(bool ascending)
    {
        var rule = new FixedSampleRule(10, 7, ascending);
        Assert.False(rule.FavorsH1(ascending ? 6 : 4));
        Assert.True(rule.FavorsH1(ascending ? 7 : 3));
    }

    [Fact]
    public void SmallCalibrationHasKnownFirstFeasibleSize()
    {
        var result = FixedSampleCalibrator.Calibrate(new(0.2, 0.8, 0.25, 0.25), 10000, 5, 123);
        Assert.Equal(new FixedSampleRule(1, 1, true), result.Rule);
        Assert.Equal(0.2, result.ExactFalsePositive, 12);
        Assert.Equal(0.2, result.ExactFalseNegative, 12);
        Assert.True(result.ExactTargetsMet);
    }

    [Fact]
    public void CalibrationFailureIsExplicit()
    {
        Assert.Throws<InvalidOperationException>(() =>
            FixedSampleCalibrator.Calibrate(new(0.49, 0.51), 2000, 2, 42));
    }

    [Fact]
    public void ExperimentsAreRepeatableAndReportCutoffsAsInconclusive()
    {
        var options = new SimulationOptions(new(0.4, 0.6, MaxSamples: 2),
            Trials: 1000, CalibrationTrials: 2000, TrajectoriesPerScenario: 2);
        var first = SimulationRunner.Run(options);
        var second = SimulationRunner.Run(options);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
        Assert.NotEqual(first.CalibrationSeed, first.EvaluationSeed);
        foreach (var scenario in first.Scenarios)
        {
            Assert.Equal(1000, scenario.Sequential.Inconclusive.Events);
            Assert.Equal(2, scenario.Sequential.Samples.Mean);
            Assert.Equal(1000, scenario.SampleDistribution.Sum(s => s.Trials));
            Assert.Equal(0, scenario.FixedSample.Inconclusive.Events);
            Assert.All(scenario.Trajectories, t => Assert.Equal(SprtDecision.Inconclusive, t.Steps[^1].Decision));
        }
        Assert.Equal(0, first.Scenarios[0].Sequential.FalsePositive!.Events);
        Assert.Equal(1000, first.Scenarios[0].Sequential.FalsePositive!.Trials);
        Assert.Null(first.Scenarios[1].Sequential.FalsePositive);
        Assert.Null(first.Scenarios[1].Sequential.FalseNegative);
        Assert.Equal(0, first.Scenarios[2].Sequential.FalseNegative!.Events);
    }

    [Theory]
    [InlineData(0.4, 0.6)]
    [InlineData(0.6, 0.4)]
    public void EvaluationErrorsAgreeWithIndependentBinomialReference(double p0, double p1)
    {
        var result = SimulationRunner.Run(new(new(p0, p1), Trials: 10000, CalibrationTrials: 2000, TrajectoriesPerScenario: 0));
        var rule = result.Calibration.Rule;
        double fp = Binomial.Probabilities(rule.Samples, p0).Where((_, k) => rule.FavorsH1(k)).Sum();
        double fn = Binomial.Probabilities(rule.Samples, p1).Where((_, k) => !rule.FavorsH1(k)).Sum();
        Assert.Equal(fp, result.Calibration.ExactFalsePositive, 12);
        Assert.Equal(fn, result.Calibration.ExactFalseNegative, 12);
        Assert.InRange(result.Scenarios[0].FixedSample.FalsePositive!.Rate, fp - 0.015, fp + 0.015);
        Assert.InRange(result.Scenarios[2].FixedSample.FalseNegative!.Rate, fn - 0.015, fn + 0.015);
        Assert.InRange(result.Scenarios[0].Sequential.FalsePositive!.Rate, 0.025, 0.075);
        Assert.InRange(result.Scenarios[2].Sequential.FalseNegative!.Rate, 0.025, 0.075);
        Assert.All(result.Scenarios, s => Assert.Equal(10000,
            s.Sequential.FavorH0.Events + s.Sequential.FavorH1.Events + s.Sequential.Inconclusive.Events));
    }
}

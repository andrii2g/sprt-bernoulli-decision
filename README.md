# sprt-bernoulli-decision

A small .NET 10 implementation of Wald's sequential probability ratio test
for two simple, independent and identically distributed Bernoulli hypotheses.

## Build and test

Install the .NET 10 SDK, then run from this directory:

```sh
dotnet build -c Release
dotnet test -c Release
```

## Engine

```csharp
var test = new BernoulliSprt(new SprtOptions(0.4, 0.6, MaxSamples: 500));
SprtStep step = test.Observe(success: true);
```

Each observation adds `log(p1/p0)` for a success or
`log((1-p1)/(1-p0))` for a failure. The engine favors H1 at or above
`log((1-beta)/alpha)` and H0 at or below `log(beta/(1-alpha))`.
Otherwise it continues until the sample limit, then returns `Inconclusive`.
Boundary checks precede the cutoff. Observing a stopped test throws.

Probabilities must lie strictly between zero and one, hypotheses must differ,
and error targets must sum to less than one. Either hypothesis ordering works.
Wald's boundaries are approximate: the targets are not exact achieved errors.

## Simulation and explicit observations

```sh
dotnet run --project src/Sprt.Bernoulli.Cli -c Release -- simulate
dotnet run --project src/Sprt.Bernoulli.Cli -c Release -- trace --observations 1111111111
dotnet run --project src/Sprt.Bernoulli.Cli -c Release -- --help
```

The default simulation uses 10,000 calibration trials per hypothesis, followed
by 10,000 fresh evaluation trials at each of p0, the midpoint, and p1.
It writes configuration, seeds, aggregate results, a sample-count distribution,
and the first six trajectories per scenario to `reports/latest/results.json`.

Options: `--p0`, `--p1`, `--alpha`, `--beta`, `--max-samples`, `--trials`,
`--calibration-trials`, `--max-fixed-samples`, `--seed`, `--trajectories`, `--output`.
All take a value. Decimal input uses a period. Outputs are ignored by Git.
The trace command accepts the five test options plus `--observations`.
It stops at the first decision and reports any unused observations. Exhausting
a shorter input stream leaves the decision as `Continue`.

The fixed baseline searches ascending N and all count thresholds using simulated
prefixes, then freezes the first feasible N and its best cutoff. Exact binomial
errors are also reported: simulation selection can miss a true target, and the
exact check does not silently change the selected rule. Selection uses point
estimates, not confidence bounds. Calibration and evaluation use distinct
SplitMix64 streams. Within evaluation, both methods see the same trial prefix.

All error rates divide by all trials under the corresponding hypothesis.
Inconclusive outcomes are separate, not errors. Wilson 95% intervals accompany
all proportions in JSON. The midpoint has decision proportions but no
false-positive/false-negative labels. Sample summaries include every trial,
including those stopped at the cap. Savings alone do not establish superiority:
compare errors and the fraction of trials reaching a decision too.

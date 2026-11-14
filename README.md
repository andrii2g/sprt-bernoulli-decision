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
Open `reports/latest/report.html` directly in a browser: it is self-contained and
works offline. It includes scenario selection, individual/all trajectory views,
a sample slider, playback, a sample-count histogram, both methods' metrics, and
the exact baseline check. `summary.csv` provides one row per scenario/method.

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

## Example experiments

```sh
# Smaller evaluation with all default statistical settings
dotnet run --project src/Sprt.Bernoulli.Cli -c Release -- simulate --trials 1000 --output reports/small

# Force many unresolved decisions by using a short sample cap
dotnet run --project src/Sprt.Bernoulli.Cli -c Release -- simulate --max-samples 20 --output reports/capped

# Reverse the hypotheses; failure counts now favor H1
dotnet run --project src/Sprt.Bernoulli.Cli -c Release -- simulate --p0 0.6 --p1 0.4 --output reports/reversed

# Explicit stream still inside the boundaries when the cutoff is reached
dotnet run --project src/Sprt.Bernoulli.Cli -c Release -- trace --observations 101010 --max-samples 6
```

The output directory is created as needed. Reusing it replaces `results.json`,
`report.html`, and `summary.csv`. Invalid arguments or a failed baseline search
return exit code 1; use a larger `--max-fixed-samples` when calibration cannot
find a feasible rule. `--trajectories 0` saves aggregate results without paths.
Do not set trajectories above the evaluation trial count.

With the defaults and seed 42, the implementation produces:

| True p | SPRT mean samples | Fixed samples | SPRT error | Fixed error | SPRT inconclusive |
|---|---:|---:|---:|---:|---:|
| 0.40 (H0) | 36.38 | 69 | FP 3.72% | FP 4.33% | 0 / 10,000 |
| 0.50 (midpoint) | 64.65 | 69 | — | — | 0 / 10,000 |
| 0.60 (H1) | 36.67 | 69 | FN 3.75% | FN 4.43% | 0 / 10,000 |

These are reproducible simulation results, not guarantees. The selected fixed
rule is `successes >= 35` at N=69; its exact binomial FP and FN rates are both
about 4.605%. The equal-weight mean savings under H0/H1 are about 47.06%.

## Layout

- `src/Sprt.Bernoulli`: statistical engine and simulation/calibration code; no runtime packages.
- `src/Sprt.Bernoulli.Cli`: argument handling, CSV/JSON export, embedded HTML/SVG report; no runtime packages.
- `tests/Sprt.Bernoulli.Tests`: engine, probability, simulation, and export tests using xUnit.
- `docs/methodology.md`: assumptions, calibration, reproducibility, and metric definitions.

No GitHub Actions or workflows are included.

## References

- [Berkeley: Wald's Sequential Probability Ratio Test](https://ucb-stat-159-s21.github.io/site/Notes/sprt.html)
- [Harvard Edge: mathematical appendix](https://harvard-edge.github.io/cs249r_book_dev/vol3/backmatter/appendix_math.html)
- [SplitMix64 reference implementation](https://prng.di.unimi.it/splitmix64.c)

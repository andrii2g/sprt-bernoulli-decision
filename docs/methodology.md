# Methodology

## Scope and assumptions

H0 is `p = p0`; H1 is `p = p1`. Observations are independent Bernoulli draws
with a constant true probability. These are simple hypotheses about one stream,
not a two-sample A/B test, a test of a composite null, or an estimate of p.
Neither hypothesis need describe a midpoint experiment. Decisions express
relative evidence; they are not posterior probabilities or proof of a hypothesis.

The engine stores the sample count, success count, and cumulative log likelihood
ratio. A success adds `log(p1) - log(p0)`; a failure adds
`log(1-p1) - log(1-p0)`. Differences of logs avoid overflowing a probability
ratio. Summation uses compensation to reduce accumulated rounding error.
All computations use IEEE 754 doubles; probabilities extremely near each other
or near endpoints remain subject to floating-point resolution.

The lower boundary is `log(beta) - log(1-alpha)`, and the upper boundary is
`log(1-beta) - log(alpha)`. Equality counts as a crossing. Check both boundaries
before checking the maximum sample count. The outcome at the cap is
`Inconclusive` if neither boundary was crossed, without a forced guess.
The engine rejects additional observations after any terminal outcome.

Alpha is the nominal probability of favoring H1 under H0; beta is the nominal
probability of favoring H0 under H1. Wald boundaries approximate the desired
errors. Discrete overshoot and truncation mean the achieved errors and decision
coverage must be measured; the implementation does not claim exact error control
or the classical optimality result for its capped, three-outcome procedure.

## Fixed-sample calibration

The baseline always decides after N observations. If p1 > p0, evidence count is
the number of successes; otherwise it is the number of failures. In that direction
the likelihood ratio is increasing in the evidence count. A rule favors H1 when
the evidence count is at least k, and H0 otherwise, including the cutoff equality
in H1. No randomized boundary decisions are used.

1. Generate separate calibration populations under H0 and H1.
2. Extend each population's Bernoulli prefixes one observation at a time, starting at N=1.
3. At each N, form histograms and examine k=0 through N+1.
4. A candidate is feasible if its simulated FP <= alpha and FN <= beta.
5. Stop at the first feasible N. Among its feasible cutoffs, minimize
   `max(FP/alpha, FN/beta)`; ties choose the smallest k.
6. Freeze that rule and evaluate on separate streams.

This is a Monte Carlo search, so the first empirically feasible N is not
necessarily the smallest truly feasible N. Testing many candidates introduces
selection uncertainty. Wilson intervals on the calibration rows describe the
selected proportions; they are not simultaneous guarantees over the search.

For the selected rule only, compute exact binomial FP and FN probabilities.
The calculation constructs normalized probability masses outwards from the
binomial mode, then sums the relevant tails. "Exact" means the binomial model
rather than Monte Carlo; arithmetic still uses doubles. The exact check is
reported without changing the simulated selection. If it fails, the report
explicitly labels the baseline as failing the requested targets. Do not claim a
target-matched comparison from that run.

Search stops with an error if no empirical candidate succeeds through the
configured maximum N. There is no hidden fallback or unlimited search.

## Reproducibility

SplitMix64 advances a 64-bit unsigned state with wraparound. The high 53 bits
of each output map to u in [0,1); the observation is `u < p`. This is a simulation
generator, not a cryptographic source. A fixed root seed and configuration
reproduce the experiment; floating-point/runtime changes can affect boundary
cases, so retain the code revision alongside exported results for long-term use.

Seed derivation is `SplitMix64(root + stream * gamma).NextUInt64()` with
`gamma = 0x9E3779B97F4A7C15` and unsigned wraparound. Root stream 0 supplies
calibration and root stream 1 evaluation. Calibration derives streams 0 and 1
for H0/H1. Evaluation derives streams 0/1/2 for H0/midpoint/H1 and then a
separate seed for each zero-based trial. Changing the number of saved
trajectories does not change outcomes.

Within each evaluation trial both methods receive the same observation prefix.
The generator continues until both methods have finished; the sequential sample
count includes only observations consumed before its own stop. This simulation
work is not the same as the number of observations required by the SPRT.

JSON records format version, generator, configuration, derived seeds, thresholds,
the fixed rule, exact checks, aggregates, histograms, and the first configured
number of trajectories per scenario. Seeds are decimal strings to preserve all
64 bits in JavaScript. Trajectory trial numbers in JSON are zero-based; the
report displays them as one-based. Paths contain the initial n=0 state and
each consumed observation, ending at their own decision. No outcome-based
trajectory selection occurs. All trial outcomes contribute to the aggregates;
individual outcomes beyond the saved paths are not retained.

## Metric definitions

- Mean samples: total samples consumed by the method divided by scenario trials,
  including inconclusive trials. Fixed-sample counts always equal N.
- Median: middle ordered sample count, averaging the two middle counts for even trial counts.
- P95: nearest-rank 95th percentile, index `ceil(.95 * trials) - 1` in zero-based order.
- False-positive rate: `FavorH1 / trials` at p0.
- False-negative rate: `FavorH0 / trials` at p1.
- Inconclusive rate: `Inconclusive / trials`, reported separately at every true p.
- Decision coverage: `1 - inconclusive rate`.
- Sample savings: `1 - mean SPRT samples / fixed N`; negative means SPRT used more samples.
- Header mean/savings: equally weighted H0/H1 means, excluding the midpoint.

All empirical proportions include 95% Wilson score intervals with
z=1.959963984540054. Zero observed errors do not establish zero true error.
Intervals are marginal Monte Carlo intervals, not corrections for all metrics
or for calibration selection. No FP/FN label is attached to midpoint results.

## Cost

The online engine uses O(1) time and memory per observation. Calibration uses
O(C M + M²) time in the worst case, with C calibration trials and search limit M;
prefix counts and current histograms use O(C + M) memory. Evaluation cost grows
with the trials and the larger of the two stopping counts. Reports retain
O(T + K S) data per scenario: T evaluation sample counts, K saved trajectories,
and sequential cap S. Start with 1,000–10,000 trials and modest caps; close
hypotheses can require substantially larger sample counts.

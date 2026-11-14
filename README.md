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

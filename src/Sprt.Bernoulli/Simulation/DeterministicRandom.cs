namespace Sprt.Bernoulli.Simulation;

/// <summary>SplitMix64 with explicit unchecked arithmetic; not a cryptographic generator.</summary>
public sealed class DeterministicRandom(ulong seed)
{
    private ulong state = seed;

    public ulong NextUInt64()
    {
        unchecked
        {
            ulong z = (state += 0x9E3779B97F4A7C15UL);
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }

    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / 9007199254740992.0);

    public static ulong DeriveSeed(ulong root, ulong stream)
    {
        unchecked
        {
            return new DeterministicRandom(root + stream * 0x9E3779B97F4A7C15UL).NextUInt64();
        }
    }
}

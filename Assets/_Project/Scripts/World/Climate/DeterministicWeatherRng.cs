using System;

/// <summary>
/// Project-owned deterministic random stream for weather generation. Its bit-level behavior is a
/// persistence/content contract and must not be replaced with a framework random implementation.
/// </summary>
public struct DeterministicWeatherRng
{
    private const uint ZeroSeedFallback = 0xA341316Cu;
    private uint state;

    public DeterministicWeatherRng(uint seed)
    {
        if (seed == 0u)
            throw new ArgumentOutOfRangeException(nameof(seed), "A XorShift32 stream requires a nonzero seed.");

        state = seed;
    }

    public static uint DeriveSeasonSeed(uint rootSeed, long seasonInstanceIndex)
    {
        if (rootSeed == 0u)
            throw new ArgumentOutOfRangeException(nameof(rootSeed), "A weather root seed must be nonzero.");
        if (seasonInstanceIndex < 0L)
        {
            throw new ArgumentOutOfRangeException(
                nameof(seasonInstanceIndex),
                seasonInstanceIndex,
                "A season instance index cannot be negative.");
        }

        unchecked
        {
            ulong x = ((ulong)rootSeed << 32) ^ (ulong)seasonInstanceIndex;
            ulong z = x + 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;

            uint seed = (uint)z ^ (uint)(z >> 32);
            return seed != 0u ? seed : ZeroSeedFallback;
        }
    }

    public uint NextUInt()
    {
        unchecked
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }
    }

    public uint NextBelow(uint bound)
    {
        if (bound == 0u)
            throw new ArgumentOutOfRangeException(nameof(bound), "A random bound must be greater than zero.");

        uint threshold = unchecked(0u - bound) % bound;
        uint value;
        do
        {
            value = NextUInt();
        }
        while (value < threshold);

        return value % bound;
    }
}

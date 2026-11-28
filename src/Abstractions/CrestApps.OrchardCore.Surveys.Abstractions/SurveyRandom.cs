namespace CrestApps.OrchardCore.Surveys;

/// <summary>
/// A small deterministic random number generator (xoshiro128**) used to draw a response's plan, so a seed gives the
/// same draw on every runtime and in tests. It is not for security.
/// </summary>
public sealed class SurveyRandom
{
    private uint _s0;
    private uint _s1;
    private uint _s2;
    private uint _s3;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyRandom"/> class.
    /// </summary>
    /// <param name="seed">The seed.</param>
    public SurveyRandom(int seed)
    {
        // SplitMix32 spreads the seed over the four words of state.
        var x = unchecked((uint)seed);
        _s0 = SplitMix(ref x);
        _s1 = SplitMix(ref x);
        _s2 = SplitMix(ref x);
        _s3 = SplitMix(ref x);

        if ((_s0 | _s1 | _s2 | _s3) == 0)
        {
            _s0 = 1;
        }
    }

    /// <summary>
    /// Gets the next 32 random bits.
    /// </summary>
    /// <returns>The bits.</returns>
    public uint NextUInt32()
    {
        var result = RotateLeft(_s1 * 5, 7) * 9;
        var t = _s1 << 9;

        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = RotateLeft(_s3, 11);

        return result;
    }

    /// <summary>
    /// Gets a number from 0 up to, but not including, a maximum.
    /// </summary>
    /// <param name="maxExclusive">The maximum, greater than 0.</param>
    /// <returns>The number.</returns>
    public int Next(int maxExclusive)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxExclusive);

        // Lemire's method without bias.
        var range = (uint)maxExclusive;
        var product = (ulong)NextUInt32() * range;
        var low = (uint)product;

        if (low < range)
        {
            var threshold = unchecked((uint)-range) % range;

            while (low < threshold)
            {
                product = (ulong)NextUInt32() * range;
                low = (uint)product;
            }
        }

        return (int)(product >> 32);
    }

    /// <summary>
    /// Shuffles a list in place with Fisher–Yates.
    /// </summary>
    /// <typeparam name="T">The type of the items.</typeparam>
    /// <param name="items">The list.</param>
    public void Shuffle<T>(IList<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    private static uint SplitMix(ref uint x)
    {
        x += 0x9E3779B9;
        var z = x;
        z = (z ^ (z >> 16)) * 0x85EBCA6B;
        z = (z ^ (z >> 13)) * 0xC2B2AE35;

        return z ^ (z >> 16);
    }

    private static uint RotateLeft(uint value, int count)
        => (value << count) | (value >> (32 - count));
}

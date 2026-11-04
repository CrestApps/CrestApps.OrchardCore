using System.Security.Cryptography;

namespace CrestApps.OrchardCore.Numbering;

/// <summary>
/// Codes people read out, type or copy from a card: upper-case letters and digits without <c>0</c>, <c>O</c>, <c>1</c>
/// and <c>I</c>, which are easy to misread. Use it for coupon, gift card and referral codes, and with
/// <see cref="INumberSequence"/> for document numbers that must never repeat.
/// </summary>
public static class ReadableCodes
{
    /// <summary>
    /// The characters a code is made of: 32 of them, so each character holds 5 bits.
    /// </summary>
    public const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    /// <summary>
    /// The longest code <see cref="FromNumber"/> writes: 12 characters hold 60 bits.
    /// </summary>
    public const int MaxNumberLength = 12;

    private const int BitsPerCharacter = 5;

    // An odd multiplier and a mask: multiplying by an odd number and then xoring are both one-to-one modulo a power of
    // two, so every number below the limit gets its own code, and consecutive numbers do not look consecutive.
    private const long Multiplier = 0x5DEECE66D;
    private const long Mask = 0x2545F4914F6CDD1D;

    private static readonly char[] _alphabet = Alphabet.ToCharArray();

    /// <summary>
    /// Returns a new random code, from a cryptographic random number generator.
    /// </summary>
    /// <param name="length">The number of characters.</param>
    /// <returns>The code.</returns>
    public static string Generate(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);

        return new string(RandomNumberGenerator.GetItems<char>(_alphabet, length));
    }

    /// <summary>
    /// Returns the code of a number, for document numbers that come from an <see cref="INumberSequence"/>. Every number
    /// from 0 to 32 to the power of <paramref name="length"/>, minus 1, gets its own code, so a code repeats only when its
    /// number does.
    /// </summary>
    /// <param name="number">The number, such as the next number of a series.</param>
    /// <param name="length">The number of characters, from 1 to <see cref="MaxNumberLength"/>.</param>
    /// <returns>The code.</returns>
    public static string FromNumber(long number, int length)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(length, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, MaxNumberLength);

        var bits = length * BitsPerCharacter;
        var limit = 1L << bits;

        ArgumentOutOfRangeException.ThrowIfNegative(number);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(number, limit);

        var value = unchecked((number * Multiplier) ^ Mask) & (limit - 1);
        var code = new char[length];

        for (var position = length - 1; position >= 0; position--)
        {
            code[position] = _alphabet[value & (_alphabet.Length - 1)];
            value >>= BitsPerCharacter;
        }

        return new string(code);
    }
}

using System.Security.Cryptography;

namespace CrestApps.Core.Support;

/// <summary>
/// Generates the opaque identifiers the framework stamps on the records it creates.
/// </summary>
/// <remarks>
/// An identifier is 26 lowercase base32 characters, which is the width every index column that
/// stores one is declared with. Identifier generation is a pure utility rather than a host seam:
/// the value carries no meaning beyond being unique, so a host that generates its own identifiers
/// in the same shape and a record created here can sit in the same column.
/// </remarks>
public static class IdentifierGenerator
{
    // The alphabet is the lowercase base32 one, chosen so an identifier survives a case-insensitive
    // comparison, a URL, and a database collation without changing.
    private const string Alphabet = "0123456789abcdefghijklmnopqrstuv";

    private const int ByteCount = 16;

    private const int CharacterCount = 26;

    /// <summary>
    /// Generates a new identifier.
    /// </summary>
    /// <returns>A 26-character lowercase base32 identifier.</returns>
    public static string Generate()
    {
        Span<byte> bytes = stackalloc byte[ByteCount];

        RandomNumberGenerator.Fill(bytes);

        return string.Create(CharacterCount, new BufferState(bytes), static (span, state) =>
        {
            // 16 bytes is 128 bits, which base32 renders in 26 characters with the last one carrying
            // the three leftover bits.
            ulong high = state.High;
            ulong low = state.Low;

            for (var i = CharacterCount - 1; i >= 0; i--)
            {
                span[i] = Alphabet[(int)(low & 0x1F)];

                low = (low >> 5) | (high << 59);
                high >>= 5;
            }
        });
    }

    private readonly struct BufferState
    {
        public BufferState(ReadOnlySpan<byte> bytes)
        {
            High = BitConverter.ToUInt64(bytes[..8]);
            Low = BitConverter.ToUInt64(bytes[8..]);
        }

        public ulong High { get; }

        public ulong Low { get; }
    }
}

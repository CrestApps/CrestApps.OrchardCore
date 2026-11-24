using System.Buffers.Text;
using System.IO.Compression;

namespace CrestApps.OrchardCore.Learning.Credentials.Core.OpenBadges;

/// <summary>
/// The W3C Bitstring Status List encoding: a bitstring whose index 0 is the left-most bit of the first byte, compressed
/// with GZIP, encoded in base64url without padding and prefixed with <c>u</c> (multibase).
/// </summary>
public static class BitstringStatusList
{
    private const char _multibasePrefix = 'u';

    /// <summary>
    /// Encodes a list whose set bits are given.
    /// </summary>
    /// <param name="setIndexes">The indexes whose bit is set.</param>
    /// <param name="size">The number of bits.</param>
    /// <returns>The encoded list.</returns>
    public static string Encode(IEnumerable<int> setIndexes, int size)
    {
        ArgumentNullException.ThrowIfNull(setIndexes);

        if (size <= 0 || size % 8 != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, "The size must be a positive multiple of 8.");
        }

        var bits = new byte[size / 8];

        foreach (var index in setIndexes)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index, nameof(setIndexes));
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, size, nameof(setIndexes));

            bits[index >> 3] |= (byte)(0x80 >> (index & 7));
        }

        using var output = new MemoryStream();

        using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            gzip.Write(bits);
        }

        return _multibasePrefix + Base64Url.EncodeToString(output.ToArray());
    }

    /// <summary>
    /// Reads a bit of an encoded list.
    /// </summary>
    /// <param name="encodedList">The encoded list.</param>
    /// <param name="index">The index.</param>
    /// <returns><see langword="true"/> when the bit is set.</returns>
    public static bool IsSet(string encodedList, int index)
    {
        var bits = Decode(encodedList);

        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, bits.Length * 8);

        return (bits[index >> 3] & (0x80 >> (index & 7))) != 0;
    }

    private static byte[] Decode(string encodedList)
    {
        if (string.IsNullOrEmpty(encodedList) || encodedList[0] != _multibasePrefix)
        {
            throw new FormatException("The list is not multibase base64url.");
        }

        using var input = new MemoryStream(Base64Url.DecodeFromChars(encodedList.AsSpan(1)));
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();

        gzip.CopyTo(output);

        return output.ToArray();
    }
}

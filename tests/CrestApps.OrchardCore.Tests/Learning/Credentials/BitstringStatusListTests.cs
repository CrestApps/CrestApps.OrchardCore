using System.Buffers.Text;
using System.IO.Compression;
using CrestApps.OrchardCore.Learning.Credentials.Core.OpenBadges;

namespace CrestApps.OrchardCore.Tests.Learning.Credentials;

/// <summary>
/// Phase 8, slice 5b: the W3C Bitstring Status List encoding (section 3.1 and 3.2): a multibase base64url (no padding)
/// GZIP-compressed bitstring of at least 16 KB, index 0 the left-most bit.
/// </summary>
public sealed class BitstringStatusListTests
{
    // The encoded list of the specification's example status list credential: an empty 16 KB list.
    private const string SpecificationExample = "uH4sIAAAAAAAAA-3BMQEAAADCoPVPbQwfoAAAAAAAAAAAAAAAAAAAAIC3AYbSVKsAQAAA";

    [Fact]
    public void Encode_IsMultibaseBase64Url_OfAGzippedBitstringOfTheSize()
    {
        // Act
        var encoded = BitstringStatusList.Encode([], 131072);
        var bytes = Gunzip(encoded);

        // Assert
        Assert.StartsWith("u", encoded, StringComparison.Ordinal);
        Assert.DoesNotContain("=", encoded, StringComparison.Ordinal);
        Assert.DoesNotContain("+", encoded, StringComparison.Ordinal);
        Assert.DoesNotContain("/", encoded, StringComparison.Ordinal);
        Assert.Equal(16384, bytes.Length);
        Assert.All(bytes, value => Assert.Equal(0, value));
    }

    [Fact]
    public void Encode_IndexZero_IsTheLeftMostBitOfTheFirstByte()
    {
        // Act
        var bytes = Gunzip(BitstringStatusList.Encode([0, 9, 131071], 131072));

        // Assert
        Assert.Equal(0b1000_0000, bytes[0]);
        Assert.Equal(0b0100_0000, bytes[1]);
        Assert.Equal(0b0000_0001, bytes[16383]);
    }

    [Fact]
    public void IsSet_ReadsWhatEncodeWrote()
    {
        // Arrange
        var encoded = BitstringStatusList.Encode([5, 94567], 131072);

        // Act and assert
        Assert.True(BitstringStatusList.IsSet(encoded, 5));
        Assert.True(BitstringStatusList.IsSet(encoded, 94567));
        Assert.False(BitstringStatusList.IsSet(encoded, 6));
        Assert.False(BitstringStatusList.IsSet(encoded, 0));
    }

    [Fact]
    public void IsSet_ReadsTheSpecificationExample()
    {
        // Act and assert: the example is an empty list, so no bit is set.
        Assert.False(BitstringStatusList.IsSet(SpecificationExample, 0));
        Assert.False(BitstringStatusList.IsSet(SpecificationExample, 94567));
        Assert.Equal(16384, Gunzip(SpecificationExample).Length);
    }

    [Fact]
    public void Encode_RefusesAnIndexOutsideTheList()
    {
        // Act and assert
        Assert.Throws<ArgumentOutOfRangeException>(() => BitstringStatusList.Encode([131072], 131072));
        Assert.Throws<ArgumentOutOfRangeException>(() => BitstringStatusList.IsSet(BitstringStatusList.Encode([], 131072), 131072));
    }

    private static byte[] Gunzip(string encoded)
    {
        using var input = new MemoryStream(Base64Url.DecodeFromChars(encoded.AsSpan(1)));
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();

        gzip.CopyTo(output);

        return output.ToArray();
    }
}

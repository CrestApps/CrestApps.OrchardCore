using CrestApps.OrchardCore.AgenticCommerce.Core.Security;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce;

/// <summary>
/// The ACP <c>Merchant-Signature</c> header signs <c>t + "." + body</c> with HMAC-SHA256 and rejects old or altered requests.
/// </summary>
public sealed class TimestampedHmacSignatureTests
{
    private const string Secret = "whsec_test";

    private const string Body = "{\"type\":\"order_create\"}";

    [Fact]
    public void Sign_KnownVector_MatchesTheExpectedHeader()
    {
        // Act
        var header = TimestampedHmacSignature.Sign(Secret, 1_700_000_000, Body);

        // Assert
        Assert.Equal("t=1700000000,v1=a41638ae4344c7e8af50837cc3064044baae848b86b661916bb6894b345c1b8f", header);
    }

    [Fact]
    public void Verify_WithinTolerance_Succeeds()
    {
        // Arrange
        var header = TimestampedHmacSignature.Sign(Secret, 1_700_000_000, Body);

        // Act and Assert
        Assert.True(TimestampedHmacSignature.Verify(header, Secret, Body, 1_700_000_299));
        Assert.True(TimestampedHmacSignature.Verify(header.ToUpperInvariant().Replace("T=", "t=").Replace("V1=", "v1="), Secret, Body, 1_700_000_000));
    }

    [Theory]
    [InlineData("t=1700000000,v1=a41638ae4344c7e8af50837cc3064044baae848b86b661916bb6894b345c1b8f", "{\"type\":\"order_update\"}", 1_700_000_000)]
    [InlineData("t=1700000000,v1=a41638ae4344c7e8af50837cc3064044baae848b86b661916bb6894b345c1b8f", Body, 1_700_000_301)]
    [InlineData("t=1700000000,v1=a41638ae4344c7e8af50837cc3064044baae848b86b661916bb6894b345c1b8f", Body, 1_699_999_699)]
    [InlineData("t=1700000000", Body, 1_700_000_000)]
    [InlineData("v1=a41638ae4344c7e8af50837cc3064044baae848b86b661916bb6894b345c1b8f", Body, 1_700_000_000)]
    [InlineData("t=abc,v1=a41638ae4344c7e8af50837cc3064044baae848b86b661916bb6894b345c1b8f", Body, 1_700_000_000)]
    [InlineData("t=1700000000,v1=a41638", Body, 1_700_000_000)]
    [InlineData("garbage", Body, 1_700_000_000)]
    public void Verify_AlteredMalformedOrExpired_Fails(string header, string body, long now)
    {
        Assert.False(TimestampedHmacSignature.Verify(header, Secret, body, now));
    }

    [Fact]
    public void Verify_WrongSecret_Fails()
    {
        // Arrange
        var header = TimestampedHmacSignature.Sign(Secret, 1_700_000_000, Body);

        // Act and Assert
        Assert.False(TimestampedHmacSignature.Verify(header, "whsec_other", Body, 1_700_000_000));
        Assert.False(TimestampedHmacSignature.Verify(header, null, Body, 1_700_000_000));
    }
    [Fact]
    public void Verify_ARotatedSecret_MatchesAnyOfAFewSignatures()
    {
        // Arrange: a sender rotating its secret sends the old and the new signature.
        var header = TimestampedHmacSignature.Sign(Secret, 1_700_000_000, Body);
        var rotated = "t=1700000000,v1=" + new string('0', 64) + "," + header[(header.IndexOf(',') + 1)..];

        // Act and Assert
        Assert.True(TimestampedHmacSignature.Verify(rotated, Secret, Body, 1_700_000_000));
    }

    [Fact]
    public void Verify_AHeaderWithManySignatures_IsRefusedWithoutHashingEach()
    {
        // Arrange: DRY-7. Hundreds of v1 members would make the store hash the body once per member for an
        // unauthenticated request; a header with more than a sender ever needs is refused, valid member or not.
        var header = TimestampedHmacSignature.Sign(Secret, 1_700_000_000, Body);
        var padded = "t=1700000000," + string.Concat(Enumerable.Repeat("v1=" + new string('0', 64) + ",", 200)) + header[(header.IndexOf(',') + 1)..];

        // Act and Assert
        Assert.False(TimestampedHmacSignature.Verify(padded, Secret, Body, 1_700_000_000));
    }
}

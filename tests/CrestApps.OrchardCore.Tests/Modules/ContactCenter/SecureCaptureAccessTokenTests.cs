using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Core.Security;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// SEC-P10h: the token that opens a customer's card-capture page was two id-generator ids joined, and an id generator
/// makes no promise about unpredictability. It is 256 bits from the cryptographic generator, stored as its hash.
/// </summary>
public sealed class SecureCaptureAccessTokenTests
{
    [Fact]
    public void ACaptureToken_Is256RandomBits_StoredAsItsHash()
    {
        var (raw, hash) = SecureCaptureAccessToken.Create();
        var (other, _) = SecureCaptureAccessToken.Create();

        Assert.Equal(43, raw.Length);
        Assert.All(raw, character => Assert.True(char.IsAsciiLetterOrDigit(character) || character is '-' or '_'));
        Assert.NotEqual(raw, other);
        Assert.Equal(SecretTokens.Hash(raw), hash);
        Assert.DoesNotContain(raw, hash, StringComparison.Ordinal);
    }

    [Fact]
    public void AnExistingTokensHash_IsUnchanged()
    {
        // Links already handed out are looked up by the same lower-case hex SHA-256.
        Assert.Equal("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", SecureCaptureAccessToken.Hash("hello"));
    }
}

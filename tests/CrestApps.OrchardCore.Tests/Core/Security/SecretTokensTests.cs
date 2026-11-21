using CrestApps.OrchardCore.Core.Security;

namespace CrestApps.OrchardCore.Tests.Core.Security;

/// <summary>
/// DRY-2: seven helpers made "a random token and its SHA-256" each their own way, in four encodings, and one made a
/// card-capture token from an id generator. <see cref="SecretTokens"/> is the one place a secret token is made.
/// </summary>
public sealed class SecretTokensTests
{
    [Fact]
    public void Create_Gives256RandomBits_AsBase64Url()
    {
        var token = SecretTokens.Create();
        var other = SecretTokens.Create();

        Assert.Equal(43, token.Length);
        Assert.All(token, character => Assert.True(char.IsAsciiLetterOrDigit(character) || character is '-' or '_', $"'{character}' is not base64url."));
        Assert.NotEqual(token, other);
    }

    [Theory]
    [InlineData(16, 22)]
    [InlineData(24, 32)]
    [InlineData(48, 64)]
    public void Create_GivesTheBytesAskedFor(int bytes, int length)
        => Assert.Equal(length, SecretTokens.Create(bytes).Length);

    [Fact]
    public void Create_RefusesTooFewBytes()
        => Assert.Throws<ArgumentOutOfRangeException>(() => SecretTokens.Create(8));

    [Fact]
    public void Hash_IsTheLowerCaseHexSha256()
    {
        Assert.Equal("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", SecretTokens.Hash("hello"));
        Assert.Null(SecretTokens.Hash(null));
        Assert.Null(SecretTokens.Hash(string.Empty));
    }

    [Fact]
    public void FixedTimeEquals_ComparesValues()
    {
        Assert.True(SecretTokens.FixedTimeEquals("abc", "abc"));
        Assert.False(SecretTokens.FixedTimeEquals("abc", "abd"));
        Assert.False(SecretTokens.FixedTimeEquals("abc", "abcd"));
        Assert.False(SecretTokens.FixedTimeEquals(null, "abc"));
        Assert.False(SecretTokens.FixedTimeEquals(null, null));
    }
}

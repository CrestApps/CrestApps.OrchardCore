using CrestApps.OrchardCore.AgenticCommerce.Core.Services;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce;

/// <summary>
/// A platform's API key is recognizable by its prefix, stored only as a stable hash, and shown by its last characters.
/// </summary>
public sealed class AgentPlatformApiKeysTests
{
    [Fact]
    public void Create_StartsWithThePrefixAndIsNewEveryTime()
    {
        // Act
        var first = AgentPlatformApiKeys.Create();
        var second = AgentPlatformApiKeys.Create();

        // Assert
        Assert.StartsWith(AgentPlatformApiKeys.Prefix, first, StringComparison.Ordinal);
        Assert.Equal(AgentPlatformApiKeys.Prefix.Length + 64, first.Length);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Hash_IsStableAndDiffersFromTheKey()
    {
        // Arrange
        var key = AgentPlatformApiKeys.Create();

        // Act
        var first = AgentPlatformApiKeys.Hash(key);
        var second = AgentPlatformApiKeys.Hash(key);

        // Assert
        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
        Assert.DoesNotContain(key, first, StringComparison.Ordinal);
        Assert.NotEqual(first, AgentPlatformApiKeys.Hash(AgentPlatformApiKeys.Create()));
    }

    [Fact]
    public void Hash_OfAKnownValue_IsItsLowerCaseSha256()
    {
        // Act
        var hash = AgentPlatformApiKeys.Hash("abc");

        // Assert
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", hash);
    }

    [Theory]
    [InlineData("agk_0123456789abcdef", "cdef")]
    [InlineData("abcd", "abcd")]
    [InlineData("abc", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Hint_IsTheLastFourCharacters(string key, string expected)
    {
        // Act
        var hint = AgentPlatformApiKeys.Hint(key);

        // Assert
        Assert.Equal(expected, hint);
    }
}

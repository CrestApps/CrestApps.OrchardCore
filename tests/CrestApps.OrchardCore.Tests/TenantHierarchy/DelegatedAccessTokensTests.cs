using CrestApps.OrchardCore.TenantHierarchy.Core.Services;

namespace CrestApps.OrchardCore.Tests.TenantHierarchy;

public sealed class DelegatedAccessTokensTests
{
    [Fact]
    public void CreateToken_Has256BitsOfRandomness()
    {
        // Act
        var tokens = Enumerable.Range(0, 100).Select(_ => DelegatedAccessTokens.CreateToken()).ToList();

        // Assert
        Assert.All(tokens, token => Assert.Equal(43, token.Length));
        Assert.All(tokens, token => Assert.Matches("^[A-Za-z0-9_-]+$", token));
        Assert.Equal(tokens.Count, tokens.Distinct().Count());
    }

    [Fact]
    public void Hash_IsStableAndDiffersFromTheValue()
    {
        // Act
        var first = DelegatedAccessTokens.Hash("value");
        var second = DelegatedAccessTokens.Hash("value");
        var other = DelegatedAccessTokens.Hash("other");

        // Assert
        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
        Assert.NotEqual("value", first);
    }

    [Fact]
    public void CreateCodeChallenge_MatchesTheRfc7636Example()
    {
        // Act: the example of RFC 7636, appendix B.
        var challenge = DelegatedAccessTokens.CreateCodeChallenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk");

        // Assert
        Assert.Equal("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM", challenge);
    }

    [Fact]
    public void VerifyCodeChallenge_RightVerifier_IsTrue()
    {
        // Arrange
        var verifier = DelegatedAccessTokens.CreateToken();
        var challenge = DelegatedAccessTokens.CreateCodeChallenge(verifier);

        // Act
        var verified = DelegatedAccessTokens.VerifyCodeChallenge(verifier, challenge);

        // Assert
        Assert.True(verified);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("another-verifier")]
    public void VerifyCodeChallenge_WrongOrMissingVerifier_IsFalse(string verifier)
    {
        // Arrange
        var challenge = DelegatedAccessTokens.CreateCodeChallenge(DelegatedAccessTokens.CreateToken());

        // Act
        var verified = DelegatedAccessTokens.VerifyCodeChallenge(verifier, challenge);

        // Assert
        Assert.False(verified);
    }

    [Fact]
    public void VerifyCodeChallenge_MissingChallenge_IsFalse()
    {
        // Act
        var verified = DelegatedAccessTokens.VerifyCodeChallenge("verifier", null);

        // Assert
        Assert.False(verified);
    }

    [Fact]
    public void GetReference_IsAShortPrefix()
    {
        // Act
        var reference = DelegatedAccessTokens.GetReference("abcdefghijklmnop");

        // Assert
        Assert.Equal("abcdefgh", reference);
        Assert.Null(DelegatedAccessTokens.GetReference(null));
        Assert.Equal("abc", DelegatedAccessTokens.GetReference("abc"));
    }

    [Fact]
    public void ComputeRolesVersion_IgnoresOrderCaseAndDuplicates()
    {
        // Act
        var first = DelegatedAccessTokens.ComputeRolesVersion(["Editor", "Administrator"]);
        var second = DelegatedAccessTokens.ComputeRolesVersion(["administrator", "EDITOR", "Editor"]);
        var other = DelegatedAccessTokens.ComputeRolesVersion(["Editor"]);

        // Assert
        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
        Assert.Equal(16, first.Length);
    }

    [Fact]
    public void ComputeRolesVersion_NoRoles_IsStable()
    {
        // Act
        var version = DelegatedAccessTokens.ComputeRolesVersion(null);

        // Assert
        Assert.Equal(DelegatedAccessTokens.ComputeRolesVersion([]), version);
    }
}

using CrestApps.OrchardCore.Asterisk.Services;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class AsteriskAriApplicationOwnershipRegistryTests
{
    [Fact]
    public void TryClaim_FirstClaim_ReturnsTrue()
    {
        // Arrange
        var registry = new AsteriskAriApplicationOwnershipRegistry();
        var uniqueId = Guid.NewGuid().ToString("N");
        var baseUrl = $"http://pbx-{uniqueId}.test:8088/ari/";
        var app = $"app-{uniqueId}";

        // Act
        var result = registry.TryClaim(baseUrl, app, "TenantA", Token());

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void TryClaim_SameTenantSameTokenSecondClaim_ReturnsTrue()
    {
        // Arrange
        var registry = new AsteriskAriApplicationOwnershipRegistry();
        var uniqueId = Guid.NewGuid().ToString("N");
        var baseUrl = $"http://pbx-{uniqueId}.test:8088/ari/";
        var app = $"app-{uniqueId}";
        var token = Token();
        registry.TryClaim(baseUrl, app, "TenantA", token);

        // Act
        var result = registry.TryClaim(baseUrl, app, "TenantA", token);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void TryClaim_SameTenantDifferentToken_ReturnsTrue()
    {
        // Arrange
        // A second generation of the same tenant (a different generation token) claiming the same pair must succeed
        // and reference-count alongside the first generation.
        var registry = new AsteriskAriApplicationOwnershipRegistry();
        var uniqueId = Guid.NewGuid().ToString("N");
        var baseUrl = $"http://pbx-{uniqueId}.test:8088/ari/";
        var app = $"app-{uniqueId}";
        registry.TryClaim(baseUrl, app, "TenantA", Token());

        // Act
        var result = registry.TryClaim(baseUrl, app, "TenantA", Token());

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void TryClaim_DifferentTenant_ReturnsFalse()
    {
        // Arrange
        var registry = new AsteriskAriApplicationOwnershipRegistry();
        var uniqueId = Guid.NewGuid().ToString("N");
        var baseUrl = $"http://pbx-{uniqueId}.test:8088/ari/";
        var app = $"app-{uniqueId}";
        registry.TryClaim(baseUrl, app, "TenantA", Token());

        // Act
        var result = registry.TryClaim(baseUrl, app, "TenantB", Token());

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("http://pbx.test:8088/ari", "http://pbx.test:8088/ari/")]
    [InlineData("http://PBX.TEST:8088/ari/", "http://pbx.test:8088/ari/")]
    [InlineData("http://pbx.test:8088/", "http://pbx.test:8088/ari/")]
    public void TryClaim_UrlNormalization_SameServerCollides(string firstBaseUrl, string secondBaseUrl)
    {
        // Arrange
        // Two URL variations that differ only by trailing slash or host casing must normalize to the same key so
        // that a second tenant attempting to claim via the alternate form is correctly rejected.
        var registry = new AsteriskAriApplicationOwnershipRegistry();
        var app = $"app-{Guid.NewGuid():N}";
        registry.TryClaim(firstBaseUrl, app, "TenantA", Token());

        // Act
        var result = registry.TryClaim(secondBaseUrl, app, "TenantB", Token());

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void TryClaim_DifferentAppSameServer_BothSucceed()
    {
        // Arrange
        var registry = new AsteriskAriApplicationOwnershipRegistry();
        var uniqueId = Guid.NewGuid().ToString("N");
        var baseUrl = $"http://pbx-{uniqueId}.test:8088/ari/";

        // Act
        var resultA = registry.TryClaim(baseUrl, $"app-alpha-{uniqueId}", "TenantA", Token());
        var resultB = registry.TryClaim(baseUrl, $"app-beta-{uniqueId}", "TenantB", Token());

        // Assert
        Assert.True(resultA);
        Assert.True(resultB);
    }

    [Fact]
    public void TryClaim_DifferentServerSameApp_BothSucceed()
    {
        // Arrange
        var registry = new AsteriskAriApplicationOwnershipRegistry();
        var uniqueId = Guid.NewGuid().ToString("N");
        var app = $"app-{uniqueId}";

        // Act
        var resultA = registry.TryClaim($"http://pbx-a-{uniqueId}.test:8088/ari/", app, "TenantA", Token());
        var resultB = registry.TryClaim($"http://pbx-b-{uniqueId}.test:8088/ari/", app, "TenantB", Token());

        // Assert
        Assert.True(resultA);
        Assert.True(resultB);
    }

    [Fact]
    public void Release_AfterRelease_AllowsOtherTenantToClaim()
    {
        // Arrange
        var registry = new AsteriskAriApplicationOwnershipRegistry();
        var uniqueId = Guid.NewGuid().ToString("N");
        var baseUrl = $"http://pbx-{uniqueId}.test:8088/ari/";
        var app = $"app-{uniqueId}";
        var tokenA = Token();
        registry.TryClaim(baseUrl, app, "TenantA", tokenA);

        // Act
        registry.Release(tokenA);
        var result = registry.TryClaim(baseUrl, app, "TenantB", Token());

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Release_WhenOverlappingGenerationsHoldPair_KeepsOwnershipUntilLastTokenReleases()
    {
        // Arrange
        // Simulate an Orchard shell reload where a retiring generation (tokenA1) and an activating generation
        // (tokenA2) of the SAME tenant overlap. The retiring generation releasing its token must NOT free the pair
        // while the activating generation is still running, otherwise a different tenant could claim it mid-reload.
        var registry = new AsteriskAriApplicationOwnershipRegistry();
        var uniqueId = Guid.NewGuid().ToString("N");
        var baseUrl = $"http://pbx-{uniqueId}.test:8088/ari/";
        var app = $"app-{uniqueId}";
        var tokenA1 = Token();
        var tokenA2 = Token();
        registry.TryClaim(baseUrl, app, "TenantA", tokenA1);
        registry.TryClaim(baseUrl, app, "TenantA", tokenA2);

        // Act
        registry.Release(tokenA1);
        var claimedDuringOverlap = registry.TryClaim(baseUrl, app, "TenantB", Token());

        registry.Release(tokenA2);
        var claimedAfterFullRelease = registry.TryClaim(baseUrl, app, "TenantB", Token());

        // Assert
        Assert.False(claimedDuringOverlap);
        Assert.True(claimedAfterFullRelease);
    }

    [Fact]
    public void Release_OnlyReleasesPairsHeldByTheGivenToken()
    {
        // Arrange
        var registry = new AsteriskAriApplicationOwnershipRegistry();
        var uniqueId = Guid.NewGuid().ToString("N");
        var baseUrl = $"http://pbx-{uniqueId}.test:8088/ari/";
        var appAlpha = $"app-alpha-{uniqueId}";
        var appBeta = $"app-beta-{uniqueId}";
        var tokenAlpha = Token();
        var tokenBeta = Token();
        registry.TryClaim(baseUrl, appAlpha, "TenantA", tokenAlpha);
        registry.TryClaim(baseUrl, appBeta, "TenantA", tokenBeta);

        // Act
        registry.Release(tokenAlpha);
        var alphaReclaimed = registry.TryClaim(baseUrl, appAlpha, "TenantB", Token());
        var betaStillOwned = registry.TryClaim(baseUrl, appBeta, "TenantB", Token());

        // Assert
        Assert.True(alphaReclaimed);
        Assert.False(betaStillOwned);
    }

    [Fact]
    public void Release_WhenTokenIsNullOrWhitespace_DoesNotThrowAndKeepsOwnership()
    {
        // Arrange
        var registry = new AsteriskAriApplicationOwnershipRegistry();
        var uniqueId = Guid.NewGuid().ToString("N");
        var baseUrl = $"http://pbx-{uniqueId}.test:8088/ari/";
        var app = $"app-{uniqueId}";
        registry.TryClaim(baseUrl, app, "TenantA", Token());

        // Act
        registry.Release(null);
        registry.Release("   ");
        var result = registry.TryClaim(baseUrl, app, "TenantB", Token());

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsOwnedByAnotherTenant_WhenOwnedByOtherTenant_ReturnsTrue()
    {
        // Arrange
        var registry = new AsteriskAriApplicationOwnershipRegistry();
        var uniqueId = Guid.NewGuid().ToString("N");
        var baseUrl = $"http://pbx-{uniqueId}.test:8088/ari/";
        var app = $"app-{uniqueId}";
        registry.TryClaim(baseUrl, app, "TenantA", Token());

        // Act
        var result = registry.IsOwnedByAnotherTenant(baseUrl, app, "TenantB");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsOwnedByAnotherTenant_WhenOwnedBySameTenant_ReturnsFalse()
    {
        // Arrange
        var registry = new AsteriskAriApplicationOwnershipRegistry();
        var uniqueId = Guid.NewGuid().ToString("N");
        var baseUrl = $"http://pbx-{uniqueId}.test:8088/ari/";
        var app = $"app-{uniqueId}";
        registry.TryClaim(baseUrl, app, "TenantA", Token());

        // Act
        var result = registry.IsOwnedByAnotherTenant(baseUrl, app, "TenantA");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsOwnedByAnotherTenant_WhenUnclaimed_ReturnsFalse()
    {
        // Arrange
        var registry = new AsteriskAriApplicationOwnershipRegistry();
        var uniqueId = Guid.NewGuid().ToString("N");
        var baseUrl = $"http://pbx-{uniqueId}.test:8088/ari/";
        var app = $"app-{uniqueId}";

        // Act
        var result = registry.IsOwnedByAnotherTenant(baseUrl, app, "TenantA");

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(null, "contact-center")]
    [InlineData("", "contact-center")]
    [InlineData("http://pbx.test:8088/ari/", null)]
    [InlineData("http://pbx.test:8088/ari/", "")]
    public void IsOwnedByAnotherTenant_WhenBaseUrlOrApplicationNameIsBlank_ReturnsFalse(string baseUrl, string app)
    {
        // Arrange
        var registry = new AsteriskAriApplicationOwnershipRegistry();

        // Act
        var result = registry.IsOwnedByAnotherTenant(baseUrl, app, "TenantA");

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(null, "contact-center", "token")]
    [InlineData("", "contact-center", "token")]
    [InlineData("   ", "contact-center", "token")]
    [InlineData("http://pbx.test:8088/ari/", null, "token")]
    [InlineData("http://pbx.test:8088/ari/", "", "token")]
    [InlineData("http://pbx.test:8088/ari/", "   ", "token")]
    [InlineData("http://pbx.test:8088/ari/", "contact-center", null)]
    [InlineData("http://pbx.test:8088/ari/", "contact-center", "")]
    [InlineData("http://pbx.test:8088/ari/", "contact-center", "   ")]
    public void TryClaim_WhenBaseUrlApplicationNameOrTokenIsNullOrWhitespace_ReturnsTrue(string baseUrl, string app, string token)
    {
        // Arrange
        // An unconfigured tenant (blank BaseUrl, ApplicationName, or token) starts no listener; returning true is
        // safe because the tenant will never compete for ARI events.
        var registry = new AsteriskAriApplicationOwnershipRegistry();

        // Act
        var result = registry.TryClaim(baseUrl, app, "TenantA", token);

        // Assert
        Assert.True(result);
    }

    private static string Token()
        => Guid.NewGuid().ToString("N");
}

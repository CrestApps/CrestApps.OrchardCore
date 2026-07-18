using CrestApps.OrchardCore.Asterisk;
using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.Asterisk.Services;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class AsteriskAriApplicationGateTests
{
    [Fact]
    public void TryAcquire_WhenApplicationIsUnclaimed_ReturnsTrue()
    {
        // Arrange
        var gate = CreateGate(new AsteriskAriApplicationOwnershipRegistry(), "TenantA");
        var settings = CreateSettings();

        // Act
        var acquired = gate.TryAcquire(settings);

        // Assert
        Assert.True(acquired);
    }

    [Fact]
    public void TryAcquire_WhenAnotherTenantOwnsTheApplication_ReturnsFalse()
    {
        // Arrange
        var settings = CreateSettings();

        Assert.True(new AsteriskAriApplicationOwnershipRegistry()
            .TryClaim(settings.BaseUrl, settings.ApplicationName, "OwnerTenant", Token()));

        var gate = CreateGate(new AsteriskAriApplicationOwnershipRegistry(), "TenantB");

        // Act
        var acquired = gate.TryAcquire(settings);

        // Assert
        Assert.False(acquired);
    }

    [Fact]
    public void TryAcquire_WhenSettingsAreNull_ReturnsTrue()
    {
        // Arrange
        var gate = CreateGate(new AsteriskAriApplicationOwnershipRegistry(), "TenantA");

        // Act
        var acquired = gate.TryAcquire(null);

        // Assert
        Assert.True(acquired);
    }

    [Fact]
    public void TryAcquire_WhenNonDefaultTenantCollidesWithHostDefaultApplication_ReturnsFalse()
    {
        // Arrange
        // A non-default tenant that resolves to the same base URL and application name as the enabled host default
        // would cross-deliver Stasis events with the default shell's listener, so the gate must deny it.
        var baseUrl = UniqueBaseUrl();
        const string applicationName = "host-default-app";
        var gate = CreateGate(new AsteriskAriApplicationOwnershipRegistry(), "TenantA", new DefaultAsteriskOptions
        {
            IsEnabled = true,
            BaseUrl = baseUrl,
            ApplicationName = applicationName,
        });

        // Act
        var acquired = gate.TryAcquire(CreateSettings(baseUrl, applicationName));

        // Assert
        Assert.False(acquired);
    }

    [Fact]
    public void TryAcquire_WhenDefaultShellUsesHostDefaultApplication_ReturnsTrue()
    {
        // Arrange
        // The default shell owns the host-default application, so resolving to it is not a cross-tenant collision.
        var baseUrl = UniqueBaseUrl();
        const string applicationName = "host-default-app";
        var gate = CreateGate(new AsteriskAriApplicationOwnershipRegistry(), "Default", new DefaultAsteriskOptions
        {
            IsEnabled = true,
            BaseUrl = baseUrl,
            ApplicationName = applicationName,
        });

        // Act
        var acquired = gate.TryAcquire(CreateSettings(baseUrl, applicationName));

        // Assert
        Assert.True(acquired);
    }

    [Fact]
    public void IsAvailable_DoesNotClaimOwnership()
    {
        // Arrange
        // IsAvailable is a read-only probe: it must not claim the application, so a different tenant can still
        // acquire it afterwards.
        var settings = CreateSettings();
        var probeGate = CreateGate(new AsteriskAriApplicationOwnershipRegistry(), "TenantA");
        var acquiringGate = CreateGate(new AsteriskAriApplicationOwnershipRegistry(), "TenantB");

        // Act
        var available = probeGate.IsAvailable(settings);
        var acquiredByOther = acquiringGate.TryAcquire(settings);

        // Assert
        Assert.True(available);
        Assert.True(acquiredByOther);
    }

    [Fact]
    public void IsAvailable_WhenAnotherTenantOwnsTheApplication_ReturnsFalse()
    {
        // Arrange
        var settings = CreateSettings();

        Assert.True(new AsteriskAriApplicationOwnershipRegistry()
            .TryClaim(settings.BaseUrl, settings.ApplicationName, "OwnerTenant", Token()));

        var gate = CreateGate(new AsteriskAriApplicationOwnershipRegistry(), "TenantB");

        // Act
        var available = gate.IsAvailable(settings);

        // Assert
        Assert.False(available);
    }

    [Fact]
    public void ReleaseGeneration_FreesTheClaimForAnotherTenant()
    {
        // Arrange
        var settings = CreateSettings();
        var owningGate = CreateGate(new AsteriskAriApplicationOwnershipRegistry(), "TenantA");
        var contendingGate = CreateGate(new AsteriskAriApplicationOwnershipRegistry(), "TenantB");

        Assert.True(owningGate.TryAcquire(settings));
        Assert.False(contendingGate.TryAcquire(settings));

        // Act
        owningGate.ReleaseGeneration();

        // Assert
        Assert.True(contendingGate.TryAcquire(settings));
    }

    [Fact]
    public void ReleaseGeneration_WhenOverlappingGenerationStillHoldsToken_KeepsOwnership()
    {
        // Arrange
        // Two overlapping generations of the same tenant (a shell reload) each hold the claim through their own
        // per-generation token. Releasing the retiring generation must not free the application while the new
        // generation still owns it, so a different tenant stays blocked until the last generation releases.
        var settings = CreateSettings();
        var retiringGeneration = CreateGate(new AsteriskAriApplicationOwnershipRegistry(), "TenantA");
        var newGeneration = CreateGate(new AsteriskAriApplicationOwnershipRegistry(), "TenantA");
        var contendingGate = CreateGate(new AsteriskAriApplicationOwnershipRegistry(), "TenantB");

        Assert.True(retiringGeneration.TryAcquire(settings));
        Assert.True(newGeneration.TryAcquire(settings));

        // Act
        retiringGeneration.ReleaseGeneration();

        // Assert
        Assert.False(contendingGate.TryAcquire(settings));

        newGeneration.ReleaseGeneration();
        Assert.True(contendingGate.TryAcquire(settings));
    }

    private static AsteriskAriApplicationGate CreateGate(
        IAsteriskAriApplicationOwnershipRegistry registry,
        string shellName,
        DefaultAsteriskOptions defaultOptions = null)
    {
        return new AsteriskAriApplicationGate(
            registry,
            new ShellSettings { Name = shellName },
            Options.Create(defaultOptions ?? new DefaultAsteriskOptions()));
    }

    private static AsteriskResolvedSettings CreateSettings()
        => CreateSettings(UniqueBaseUrl(), "shared-ari-app");

    private static AsteriskResolvedSettings CreateSettings(string baseUrl, string applicationName)
    {
        return new AsteriskResolvedSettings
        {
            IsEnabled = true,
            BaseUrl = baseUrl,
            ApplicationName = applicationName,
            ProviderName = AsteriskConstants.ProviderTechnicalName,
        };
    }

    private static string UniqueBaseUrl()
        => $"http://asterisk-{Guid.NewGuid():N}.example/ari/";

    private static string Token()
        => Guid.NewGuid().ToString("N");
}

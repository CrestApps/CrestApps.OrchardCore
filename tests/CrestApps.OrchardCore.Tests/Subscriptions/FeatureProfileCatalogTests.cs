using CrestApps.OrchardCore.Subscriptions.Core.Services;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Models;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// SEC-C18: Orchard Core resolves a tenant's <c>FeatureProfile</c> by the profiles document's key only
/// (<c>FeatureProfilesValidationProvider</c>), and a name it cannot find allows every feature. The <c>FeatureProfiles</c>
/// recipe step keys a profile by its JSON property name, so its <see cref="FeatureProfile.Id"/> can differ; a plan that
/// named that id passed the purchase rules and sold an unrestricted tenant.
/// </summary>
public sealed class FeatureProfileCatalogTests
{
    [Fact]
    public async Task GetProfileIdsAsync_ReturnsTheNamesOrchardCoreResolves_NotAProfilesOwnId()
    {
        // Arrange: what the FeatureProfiles recipe step stores for { "basic": { "Id": "legacy-id", ... } }.
        var catalog = new FeatureProfilesTenantFeatureProfileCatalog(new FixedFeatureProfilesService(new Dictionary<string, FeatureProfile>(StringComparer.OrdinalIgnoreCase)
        {
            ["basic"] = new FeatureProfile { Id = "legacy-id", Name = "Basic" },
        }));

        // Act
        var names = await catalog.GetProfileIdsAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["basic"], names);
    }

    [Fact]
    public async Task TwoProfilesThatDifferOnlyInCase_AreBothReturned_SoTheExactOneIsChosen()
    {
        // Round 2 (Mild): the names came back in a case-insensitive set, so a plan naming "basic" could be provisioned
        // with "Basic", and the exact match and ambiguity checks never ran.
        var catalog = new FeatureProfilesTenantFeatureProfileCatalog(new FixedFeatureProfilesService(new Dictionary<string, FeatureProfile>(StringComparer.Ordinal)
        {
            ["Basic"] = new FeatureProfile { Name = "Basic" },
            ["basic"] = new FeatureProfile { Name = "basic" },
        }));

        // Act
        var names = await catalog.GetProfileIdsAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, names.Count);
        Assert.Equal("basic", TenantPurchaseValidator.ResolveFeatureProfile("basic", names));
        Assert.Equal("Basic", TenantPurchaseValidator.ResolveFeatureProfile("Basic", names));
        Assert.Null(TenantPurchaseValidator.ResolveFeatureProfile("BASIC", names));
    }

    // The dictionary Orchard Core's FeatureProfilesService returns: the profiles document as stored.
    private sealed class FixedFeatureProfilesService : IFeatureProfilesService
    {
        private readonly IDictionary<string, FeatureProfile> _profiles;

        public FixedFeatureProfilesService(IDictionary<string, FeatureProfile> profiles)
            => _profiles = profiles;

        public Task<IDictionary<string, FeatureProfile>> GetFeatureProfilesAsync()
            => Task.FromResult(_profiles);
    }
}

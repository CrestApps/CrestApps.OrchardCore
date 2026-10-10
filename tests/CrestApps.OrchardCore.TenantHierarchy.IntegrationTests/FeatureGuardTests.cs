using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.TenantHierarchy.IntegrationTests;

/// <summary>
/// Covers the feature guard and the features screen of a parent: blocked features never appear in a child, and the
/// forced Child feature cannot be switched off.
/// </summary>
[Collection(TenantHierarchyCollection.Name)]
public sealed class FeatureGuardTests
{
    private readonly TenantHierarchyFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="FeatureGuardTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared hierarchy.</param>
    public FeatureGuardTests(TenantHierarchyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AvailableFeatures_InChild_HideTheHierarchyAndBlockedFeatures()
    {
        // Act
        var available = await _fixture.Host.InTenantAsync(_fixture.BusinessOne.TenantName, async services =>
            (await services.GetRequiredService<IShellFeaturesManager>().GetAvailableFeaturesAsync()).Select(feature => feature.Id).ToList());

        // Assert
        Assert.DoesNotContain(TenantHierarchyConstants.Features.Child, available);
        Assert.DoesNotContain(TenantHierarchyConstants.Features.Parent, available);
        Assert.DoesNotContain(TenantHierarchyConstants.Features.Platform, available);
        Assert.DoesNotContain("OrchardCore.Deployment.Remote", available);
        Assert.DoesNotContain("OrchardCore.OpenId.Validation", available);
        Assert.DoesNotContain("OrchardCore.Tenants", available);
    }

    [Fact]
    public async Task AvailableFeatures_InParent_OfferTheParentFeatureButNotTheChildFeature()
    {
        // Act
        var available = await _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, async services =>
            (await services.GetRequiredService<IShellFeaturesManager>().GetAvailableFeaturesAsync()).Select(feature => feature.Id).ToList());

        // Assert
        Assert.Contains(TenantHierarchyConstants.Features.Parent, available);
        Assert.DoesNotContain(TenantHierarchyConstants.Features.Child, available);
        Assert.DoesNotContain("OrchardCore.OpenId.Validation", available);
    }

    [Fact]
    public async Task AvailableFeatures_InOrdinaryTenant_HideTheParentAndChildFeatures()
    {
        // Act
        var available = await _fixture.Host.InTenantAsync(TenantHierarchyFixture.Plain, async services =>
            (await services.GetRequiredService<IShellFeaturesManager>().GetAvailableFeaturesAsync()).Select(feature => feature.Id).ToList());

        // Assert
        Assert.DoesNotContain(TenantHierarchyConstants.Features.Parent, available);
        Assert.DoesNotContain(TenantHierarchyConstants.Features.Child, available);
        Assert.Contains("OrchardCore.Deployment.Remote", available);
    }

    [Fact]
    public async Task ChildFeatures_SeenFromParent_ListNoBlockedFeature()
    {
        // Act
        var features = await _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, services =>
            services.GetRequiredService<ITenantHierarchyBroker>().GetChildFeaturesAsync(_fixture.BusinessOne.EntryId));

        // Assert
        Assert.NotEmpty(features);
        Assert.DoesNotContain(features, feature => feature.Id == TenantHierarchyConstants.Features.Child);
        Assert.DoesNotContain(features, feature => feature.Id == "OrchardCore.Deployment.Remote");
        Assert.Contains(features, feature => feature.Id == "OrchardCore.Users" && feature.IsEnabled);
    }

    [Fact]
    public async Task DisablingTheChildFeature_FromParent_ChangesNothing()
    {
        // Act
        var result = await _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, services =>
            services.GetRequiredService<ChildTenantManager>().SetFeaturesAsync(_fixture.BusinessOne.EntryId, [TenantHierarchyConstants.Features.Child], enable: false));

        var enabled = await _fixture.Host.InTenantAsync(_fixture.BusinessOne.TenantName, async services =>
            (await services.GetRequiredService<IShellFeaturesManager>().GetEnabledFeaturesAsync()).Select(feature => feature.Id).ToList());

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(TenantHierarchyConstants.Features.Child, enabled);
    }

    [Fact]
    public async Task EnablingABlockedFeature_FromParent_ChangesNothing()
    {
        // Act
        var result = await _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, services =>
            services.GetRequiredService<ChildTenantManager>().SetFeaturesAsync(_fixture.BusinessOne.EntryId, ["OrchardCore.Deployment.Remote"], enable: true));

        var enabled = await _fixture.Host.InTenantAsync(_fixture.BusinessOne.TenantName, async services =>
            (await services.GetRequiredService<IShellFeaturesManager>().GetEnabledFeaturesAsync()).Select(feature => feature.Id).ToList());

        // Assert
        Assert.False(result.Succeeded);
        Assert.DoesNotContain("OrchardCore.Deployment.Remote", enabled);
    }

    [Fact]
    public async Task EnableAndDisable_AnAvailableFeature_FromParent_ChangesTheChild()
    {
        // Arrange: pick a feature the child may enable and does not have yet.
        var features = await _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, services =>
            services.GetRequiredService<ITenantHierarchyBroker>().GetChildFeaturesAsync(_fixture.BusinessTwo.EntryId));
        var preferred = new[] { "OrchardCore.Templates", "OrchardCore.Placements", "OrchardCore.Sitemaps", "OrchardCore.Widgets" };
        var candidate = preferred
            .Select(id => features.FirstOrDefault(feature => feature.Id == id && !feature.IsEnabled && !feature.IsAlwaysEnabled))
            .FirstOrDefault(feature => feature is not null);

        Assert.NotNull(candidate);

        // Act
        var enable = await _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, services =>
            services.GetRequiredService<ChildTenantManager>().SetFeaturesAsync(_fixture.BusinessTwo.EntryId, [candidate.Id], enable: true));
        var afterEnable = await IsEnabledAsync(_fixture.BusinessTwo.TenantName, candidate.Id);

        var disable = await _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, services =>
            services.GetRequiredService<ChildTenantManager>().SetFeaturesAsync(_fixture.BusinessTwo.EntryId, [candidate.Id], enable: false));
        var afterDisable = await IsEnabledAsync(_fixture.BusinessTwo.TenantName, candidate.Id);

        // Assert
        Assert.True(enable.Succeeded, enable.Error);
        Assert.True(afterEnable);
        Assert.True(disable.Succeeded, disable.Error);
        Assert.False(afterDisable);
    }

    private Task<bool> IsEnabledAsync(string tenantName, string featureId)
    {
        return _fixture.Host.InTenantAsync(tenantName, async services =>
            (await services.GetRequiredService<IShellFeaturesManager>().GetEnabledFeaturesAsync()).Any(feature => feature.Id == featureId));
    }
}

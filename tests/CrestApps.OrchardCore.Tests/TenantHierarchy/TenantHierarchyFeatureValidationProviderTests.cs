using CrestApps.OrchardCore.TenantHierarchy;
using CrestApps.OrchardCore.TenantHierarchy.Core.Guards;
using CrestApps.OrchardCore.TenantHierarchy.Core.Services;
using CrestApps.OrchardCore.TenantHierarchy.Models;

namespace CrestApps.OrchardCore.Tests.TenantHierarchy;

public sealed class TenantHierarchyFeatureValidationProviderTests
{
    [Fact]
    public async Task ChildFeature_IsNeverAvailable()
    {
        // Arrange
        var parent = TestShellSettings.Parent("firma");

        // Assert
        foreach (var settings in new[] { TestShellSettings.Default(), TestShellSettings.Ordinary("plain"), parent, TestShellSettings.Child("child", parent) })
        {
            Assert.False(await new TenantHierarchyFeatureValidationProvider(settings).IsFeatureValidAsync(TenantHierarchyConstants.Features.Child));
        }
    }

    [Fact]
    public void ParentFeature_IsAvailableOnlyInAParent()
    {
        // Arrange
        var parent = TestShellSettings.Parent("firma");

        // Assert
        Assert.True(TenantHierarchyFeatureValidationProvider.IsFeatureValid(parent, TenantHierarchyConstants.Features.Parent));
        Assert.False(TenantHierarchyFeatureValidationProvider.IsFeatureValid(TestShellSettings.Default(), TenantHierarchyConstants.Features.Parent));
        Assert.False(TenantHierarchyFeatureValidationProvider.IsFeatureValid(TestShellSettings.Ordinary("plain"), TenantHierarchyConstants.Features.Parent));
        Assert.False(TenantHierarchyFeatureValidationProvider.IsFeatureValid(TestShellSettings.Child("child", parent), TenantHierarchyConstants.Features.Parent));
    }

    [Fact]
    public void PlatformFeature_IsHiddenInParentsAndChildren()
    {
        // Arrange
        var parent = TestShellSettings.Parent("firma");

        // Assert
        Assert.True(TenantHierarchyFeatureValidationProvider.IsFeatureValid(TestShellSettings.Default(), TenantHierarchyConstants.Features.Platform));
        Assert.False(TenantHierarchyFeatureValidationProvider.IsFeatureValid(parent, TenantHierarchyConstants.Features.Platform));
        Assert.False(TenantHierarchyFeatureValidationProvider.IsFeatureValid(TestShellSettings.Child("child", parent), TenantHierarchyConstants.Features.Platform));
    }

    [Fact]
    public void OpenIdValidation_IsBlockedInParentsAndChildrenOnly()
    {
        // Arrange
        var parent = TestShellSettings.Parent("firma");
        const string feature = "OrchardCore.OpenId.Validation";

        // Assert
        Assert.True(TenantHierarchyFeatureValidationProvider.IsFeatureValid(TestShellSettings.Ordinary("plain"), feature));
        Assert.False(TenantHierarchyFeatureValidationProvider.IsFeatureValid(parent, feature));
        Assert.False(TenantHierarchyFeatureValidationProvider.IsFeatureValid(TestShellSettings.Child("child", parent), feature));
    }

    [Fact]
    public void RemoteDeployment_IsBlockedInChildrenOnly()
    {
        // Arrange
        var parent = TestShellSettings.Parent("firma");
        const string feature = "OrchardCore.Deployment.Remote";

        // Assert
        Assert.True(TenantHierarchyFeatureValidationProvider.IsFeatureValid(parent, feature));
        Assert.False(TenantHierarchyFeatureValidationProvider.IsFeatureValid(TestShellSettings.Child("child", parent), feature));
    }

    [Theory]
    [InlineData(ChildDatabaseStrategy.TablePrefixPerChild, false)]
    [InlineData(ChildDatabaseStrategy.SchemaPerChild, false)]
    [InlineData(ChildDatabaseStrategy.DatabasePerChild, true)]
    [InlineData(ChildDatabaseStrategy.SqlitePerChild, true)]
    public void SqlQueries_AreAllowedInChildrenWithTheirOwnDatabaseOnly(ChildDatabaseStrategy strategy, bool expected)
    {
        // Arrange
        var child = TestShellSettings.Child("child", TestShellSettings.Parent("firma"));
        child[TenantHierarchyConstants.SettingsKeys.DatabaseStrategy] = strategy.ToString();

        // Act
        var valid = TenantHierarchyFeatureValidationProvider.IsFeatureValid(child, "OrchardCore.Queries.Sql");

        // Assert
        Assert.Equal(expected, valid);
    }

    [Fact]
    public void PolicyBlockedFeatures_AreBlockedInTheChild()
    {
        // Arrange
        var child = TestShellSettings.Child("child", TestShellSettings.Parent("firma"));
        TenantHierarchySettingsWriter.WriteChildPolicy(child, new ParentTenantPolicy { BlockedFeatures = ["OrchardCore.Workflows.Http"] });

        // Assert
        Assert.False(TenantHierarchyFeatureValidationProvider.IsFeatureValid(child, "OrchardCore.Workflows.Http"));
        Assert.True(TenantHierarchyFeatureValidationProvider.IsFeatureValid(child, "OrchardCore.Workflows"));
    }

    [Fact]
    public void OrdinaryFeature_InAnOrdinaryTenant_IsAvailable()
    {
        // Assert
        Assert.True(TenantHierarchyFeatureValidationProvider.IsFeatureValid(TestShellSettings.Ordinary("plain"), "OrchardCore.Contents"));
        Assert.False(TenantHierarchyFeatureValidationProvider.IsFeatureValid(TestShellSettings.Ordinary("plain"), null));
    }
}

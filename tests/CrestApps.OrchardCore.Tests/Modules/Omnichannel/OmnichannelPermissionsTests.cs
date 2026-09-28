using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using OrchardCore;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel;

public sealed class OmnichannelPermissionsTests
{
    [Fact]
    public void PurgeActivity_HasCorrectProperties()
    {
        // Assert
        Assert.Equal("PurgeActivity", OmnichannelConstants.Permissions.PurgeActivity.Name);
        Assert.Equal("Purge activity", OmnichannelConstants.Permissions.PurgeActivity.Description);
    }

    [Fact]
    public void PurgeActivity_IsImpliedByManageActivities()
    {
        // Assert
        Assert.NotNull(OmnichannelConstants.Permissions.PurgeActivity.ImpliedBy);
        Assert.Contains(
            OmnichannelConstants.Permissions.ManageActivities,
            OmnichannelConstants.Permissions.PurgeActivity.ImpliedBy);
    }

    [Fact]
    public async Task GetPermissionsAsync_IncludesPurgeActivity()
    {
        // Arrange
        var provider = new PermissionProvider();

        // Act
        var permissions = await provider.GetPermissionsAsync();

        // Assert
        Assert.Contains(OmnichannelConstants.Permissions.PurgeActivity, permissions);
    }

    [Fact]
    public void GetDefaultStereotypes_DoesNotGrantPurgeActivityToAgent()
    {
        // Arrange
        var provider = new PermissionProvider();

        // Act
        var agent = provider.GetDefaultStereotypes()
            .Single(stereotype => stereotype.Name == OmnichannelConstants.AgentRole);

        // Assert
        Assert.DoesNotContain(OmnichannelConstants.Permissions.PurgeActivity, agent.Permissions);
    }

    // "Create and edit activities" guarded the activity editor but was never returned by the provider, so the Roles
    // editor could not grant it and only super users could add activities. It must be registered and granted to the
    // Administrator and Agent roles by default.
    [Fact]
    public async Task PermissionProvider_RegistersEditActivity()
    {
        // Arrange
        var provider = new PermissionProvider();

        // Act
        var permissions = await provider.GetPermissionsAsync();
        var stereotypes = provider.GetDefaultStereotypes().ToArray();
        var administrator = stereotypes.Single(stereotype => stereotype.Name == OrchardCoreConstants.Roles.Administrator);
        var agent = stereotypes.Single(stereotype => stereotype.Name == OmnichannelConstants.AgentRole);

        // Assert
        Assert.Contains(OmnichannelConstants.Permissions.EditActivity, permissions);
        Assert.Contains(OmnichannelConstants.Permissions.EditActivity, administrator.Permissions);
        Assert.Contains(OmnichannelConstants.Permissions.EditActivity, agent.Permissions);
    }

    // Static fields initialize in declaration order. When EditActivity was declared above ManageActivities, its
    // ImpliedBy captured a null instead of ManageActivities, so managing activities silently did not imply editing.
    [Fact]
    public void EditActivity_IsImpliedByManageActivities()
    {
        // Assert
        Assert.Equal("EditActivity", OmnichannelConstants.Permissions.EditActivity.Name);
        Assert.Equal("Create and edit activities", OmnichannelConstants.Permissions.EditActivity.Description);
        Assert.NotNull(OmnichannelConstants.Permissions.EditActivity.ImpliedBy);
        Assert.All(OmnichannelConstants.Permissions.EditActivity.ImpliedBy, Assert.NotNull);
        Assert.Contains(
            OmnichannelConstants.Permissions.ManageActivities,
            OmnichannelConstants.Permissions.EditActivity.ImpliedBy);
    }
}

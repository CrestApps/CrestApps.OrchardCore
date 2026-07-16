using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;

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
}

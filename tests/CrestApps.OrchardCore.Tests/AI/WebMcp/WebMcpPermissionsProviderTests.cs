using CrestApps.OrchardCore.AI.WebMcp.Services;
using OrchardCore;

namespace CrestApps.OrchardCore.Tests.AI.WebMcp;

public sealed class WebMcpPermissionsProviderTests
{
    [Fact]
    public async Task GetPermissionsAsync_ListsTheUseAndManagePermissions()
    {
        // Arrange
        var provider = new WebMcpPermissionsProvider();

        // Act
        var permissions = await provider.GetPermissionsAsync();

        // Assert
        Assert.Equal(
            ["UseWebMcpTools", "ManageWebMcpSettings"],
            permissions.Select(permission => permission.Name));
        Assert.All(permissions, permission => Assert.True(permission.IsSecurityCritical));
    }

    [Fact]
    public void GetDefaultStereotypes_GivesBothPermissionsToAdministrators()
    {
        // Arrange
        var provider = new WebMcpPermissionsProvider();

        // Act
        var stereotype = Assert.Single(provider.GetDefaultStereotypes());

        // Assert
        Assert.Equal(OrchardCoreConstants.Roles.Administrator, stereotype.Name);
        Assert.Contains(WebMcpPermissionsProvider.UseWebMcpTools, stereotype.Permissions);
        Assert.Contains(WebMcpPermissionsProvider.ManageWebMcpSettings, stereotype.Permissions);
    }
}

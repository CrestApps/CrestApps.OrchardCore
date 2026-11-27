using System.Security.Claims;
using CrestApps.OrchardCore.Users.Core.Services;
using Moq;
using OrchardCore.Security.Permissions;
using OrchardCore.Security.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Users;

/// <summary>
/// Round 2, #5: a role a purchase grants was refused only when one of its permissions was flagged security critical, and
/// Orchard Core leaves that flag off several permissions that lead to the whole site. A membership could grant a role with
/// Manage OpenID applications, whose holder makes an application with the Administrator role.
/// </summary>
public sealed class DefaultRoleGrantPolicyTests
{
    [Theory]
    [InlineData("ManageApplications")]
    [InlineData("ManageRemoteClients")]
    [InlineData("ManageTenants")]
    [InlineData("ManageFeatures")]
    [InlineData("ManageSettings")]
    [InlineData("Export")]
    [InlineData("AccessAdminPanel")]
    [InlineData("ManageSecrets")]
    public async Task ARoleWithAPermissionThatLeadsToTheWholeSite_IsSecurityCritical(string permissionName)
    {
        // Arrange: Orchard Core declares the permission without the security critical flag.
        var policy = CreatePolicy(permissionName);

        // Act
        var critical = await policy.IsSecurityCriticalAsync("Partner", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(critical);
    }

    [Fact]
    public async Task ARoleWithOrdinaryPermissions_IsNotSecurityCritical()
    {
        // Arrange
        var policy = CreatePolicy("ViewContent");

        // Act
        var critical = await policy.IsSecurityCriticalAsync("Member", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(critical);
    }

    private static DefaultRoleGrantPolicy CreatePolicy(string permissionName)
    {
        var roles = new Mock<IRoleService>();
        roles.Setup(service => service.GetRoleClaimsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Claim(Permission.ClaimType, permissionName)]);

        var permissions = new Mock<IPermissionService>();
        permissions.Setup(service => service.FindByNameAsync(permissionName))
            .ReturnsAsync(new Permission(permissionName, global::OrchardCore.Localization.LocalizationSource.Create<DefaultRoleGrantPolicyTests>("Test permission"), isSecurityCritical: false));

        return new DefaultRoleGrantPolicy(EmailSignInHarness.SystemRoles(), roles.Object, permissions.Object);
    }
}

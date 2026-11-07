using CrestApps.OrchardCore.Core.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;
using OrchardCore.Security.Services;

namespace CrestApps.OrchardCore.Tests.Core;

public sealed class RolePermissionMigrationsTests
{
    [Fact]
    public async Task GrantAsync_RoleHoldingTheReplacedPermission_GetsTheNewOne()
    {
        var manager = CreateRole("Manager", "ManageAIWebhooks");
        var editor = CreateRole("Editor", "ViewContent");
        var (services, roleManager) = CreateServices(manager, editor);

        await RolePermissionMigrations.GrantAsync(
            services,
            new Dictionary<string, string[]> { ["ManageAIWebhooks"] = ["ManageCommerceWebhooks"] },
            NullLogger.Instance);

        Assert.Contains(manager.RoleClaims, claim => claim.ClaimType == Permission.ClaimType && claim.ClaimValue == "ManageCommerceWebhooks");
        Assert.DoesNotContain(editor.RoleClaims, claim => claim.ClaimValue == "ManageCommerceWebhooks");
        roleManager.Verify(m => m.UpdateAsync(manager), Times.Once);
        roleManager.Verify(m => m.UpdateAsync(editor), Times.Never);
    }

    [Fact]
    public async Task GrantAsync_RoleThatAlreadyHasTheNewPermission_IsLeftAlone()
    {
        var manager = CreateRole("Manager", "ManageAIWebhooks", "ManageCommerceWebhooks");
        var (services, roleManager) = CreateServices(manager);

        await RolePermissionMigrations.GrantAsync(
            services,
            new Dictionary<string, string[]> { ["ManageAIWebhooks"] = ["ManageCommerceWebhooks"] },
            NullLogger.Instance);

        Assert.Single(manager.RoleClaims, claim => claim.ClaimValue == "ManageCommerceWebhooks");
        roleManager.Verify(m => m.UpdateAsync(It.IsAny<IRole>()), Times.Never);
    }

    private static Role CreateRole(string name, params string[] permissions)
        => new()
        {
            RoleName = name,
            RoleClaims = [.. permissions.Select(permission => new RoleClaim { ClaimType = Permission.ClaimType, ClaimValue = permission })],
        };

    private static (IServiceProvider Services, Mock<RoleManager<IRole>> RoleManager) CreateServices(params Role[] roles)
    {
        var roleService = new Mock<IRoleService>();
        roleService.Setup(service => service.GetRolesAsync()).ReturnsAsync(roles);

        var roleManager = new Mock<RoleManager<IRole>>(Mock.Of<IRoleStore<IRole>>(), null, null, null, null);
        roleManager.Setup(m => m.UpdateAsync(It.IsAny<IRole>())).ReturnsAsync(IdentityResult.Success);

        var services = new ServiceCollection()
            .AddSingleton(roleService.Object)
            .AddSingleton(roleManager.Object)
            .BuildServiceProvider();

        return (services, roleManager);
    }
}

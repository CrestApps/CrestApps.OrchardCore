using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Memberships;
using CrestApps.OrchardCore.Memberships.Drivers;
using CrestApps.OrchardCore.Memberships.Models;
using CrestApps.OrchardCore.Memberships.Services;
using CrestApps.OrchardCore.Memberships.ViewModels;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Modules;
using OrchardCore.Security;
using OrchardCore.Security.Services;
using OrchardCore.Entities;
using OrchardCore.Settings;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using IUserService = OrchardCore.Users.Services.IUserService;
using MembershipService = CrestApps.OrchardCore.Memberships.Services.MembershipService;

namespace CrestApps.OrchardCore.Tests.Memberships;

/// <summary>
/// SEC-C10: a membership product grants only roles a purchase may grant. The editor changes the roles only for someone
/// allowed to manage memberships and assign roles, and the grant itself never gives an excluded role (Administrator) or
/// one outside the store's allowlist, whatever was stored by a recipe or an API write.
/// </summary>
public sealed class MembershipRoleGrantTests
{
    [Fact]
    public async Task GrantLineAsync_StoredAdministratorRole_IsNeverGranted()
    {
        // Arrange
        var grants = new Mock<IRoleGrantService>();
        var service = CreateService(grants);

        // Act
        await service.GrantLineAsync(CreateOrder(), CreateLine(), new MembershipPart { RoleNames = ["Administrator", "Member"] }, TestContext.Current.CancellationToken);

        // Assert
        grants.Verify(g => g.GrantAsync("buyer-1", "Administrator", It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        grants.Verify(g => g.GrantAsync("buyer-1", "Member", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task GrantLineAsync_RoleOutsideTheAllowlist_IsNotGranted()
    {
        // Arrange
        var grants = new Mock<IRoleGrantService>();
        var service = CreateService(grants, new MembershipSettings { GrantableRoles = ["Member"] });

        // Act
        await service.GrantLineAsync(CreateOrder(), CreateLine(), new MembershipPart { RoleNames = ["Member", "Editor"] }, TestContext.Current.CancellationToken);

        // Assert
        grants.Verify(g => g.GrantAsync("buyer-1", "Editor", It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        grants.Verify(g => g.GrantAsync("buyer-1", "Member", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Driver_UserWithoutThePermission_KeepsTheStoredRoles()
    {
        // Arrange
        var part = new MembershipPart { RoleNames = ["Member"], DurationDays = 30 };
        var driver = CreateDriver(authorized: false);
        var context = CreateUpdateContext(new MembershipPartViewModel { RoleNames = ["Editor"], DurationDays = 30 });

        // Act
        await driver.UpdateAsync(part, context);

        // Assert
        Assert.Equal(["Member"], part.RoleNames);
    }

    [Fact]
    public async Task Driver_UserWithThePermission_ChangesTheRoles()
    {
        // Arrange
        var part = new MembershipPart { RoleNames = ["Member"], DurationDays = 30 };
        var driver = CreateDriver(authorized: true);
        var context = CreateUpdateContext(new MembershipPartViewModel { RoleNames = ["Editor"], DurationDays = 30 });

        // Act
        await driver.UpdateAsync(part, context);

        // Assert
        Assert.Equal(["Editor"], part.RoleNames);
    }

    private static MembershipService CreateService(Mock<IRoleGrantService> grants, MembershipSettings settings = null)
    {
        var memberships = new Mock<IMembershipStore>();
        var users = new Mock<IUserService>();
        var site = new SiteSettings();
        var siteService = new Mock<ISiteService>();

        memberships.Setup(store => store.ListByUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        users.Setup(service => service.GetUserByUniqueIdAsync("buyer-1")).ReturnsAsync(new User { UserId = "buyer-1", UserName = "buyer" });
        grants.Setup(g => g.GrantAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        site.Put(settings ?? new MembershipSettings());
        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site);

        var services = new ServiceCollection()
            .AddSingleton(memberships.Object)
            .AddSingleton(users.Object)
            .AddSingleton(grants.Object)
            .AddSingleton(siteService.Object)
            .AddSingleton(Mock.Of<IClock>(clock => clock.UtcNow == new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc)))
            .BuildServiceProvider();

        return ActivatorUtilities.CreateInstance<MembershipService>(services);
    }

    private static MembershipPartDisplayDriver CreateDriver(bool authorized)
    {
        var roles = new Mock<IRoleService>();
        var authorization = new Mock<IAuthorizationService>();
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity("test")) };

        roles.Setup(service => service.GetRolesAsync()).ReturnsAsync([new Role { RoleName = "Administrator" }, new Role { RoleName = "Member" }, new Role { RoleName = "Editor" }]);
        authorization
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(authorized ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        var site = new SiteSettings();
        var siteService = new Mock<ISiteService>();

        site.Put(new MembershipSettings());
        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site);

        var services = new ServiceCollection()
            .AddSingleton(roles.Object)
            .AddSingleton(authorization.Object)
            .AddSingleton(Mock.Of<IMembershipStore>())
            .AddSingleton(Mock.Of<IUserService>())
            .AddSingleton(Mock.Of<IRoleGrantService>())
            .AddSingleton(siteService.Object)
            .AddSingleton(Mock.Of<IClock>())
            .AddSingleton<MembershipService>()
            .AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = httpContext })
            .AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>))
            .BuildServiceProvider();

        return ActivatorUtilities.CreateInstance<MembershipPartDisplayDriver>(services);
    }

    private static UpdatePartEditorContext CreateUpdateContext(MembershipPartViewModel posted)
    {
        var definition = new ContentTypePartDefinition(nameof(MembershipPart), new ContentPartDefinition(nameof(MembershipPart)), []);

        return new UpdatePartEditorContext(definition, PostedFormUpdateModel.CreateContext(posted));
    }

    private static Order CreateOrder()
        => new() { ItemId = "order-1", UserId = "buyer-1" };

    private static OrderLine CreateLine()
        => new() { LineId = "line-1", ContentItemId = "plan", Title = "Plan", Quantity = 1 };

    private sealed class PassThroughStringLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}

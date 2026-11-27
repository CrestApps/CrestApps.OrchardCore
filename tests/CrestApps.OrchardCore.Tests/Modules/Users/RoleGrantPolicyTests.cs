using System.Security.Claims;
using CrestApps.OrchardCore.Memberships;
using CrestApps.OrchardCore.Memberships.Handlers;
using CrestApps.OrchardCore.Memberships.Models;
using CrestApps.OrchardCore.Memberships.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Handlers;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Users;
using CrestApps.OrchardCore.Users.Core;
using CrestApps.OrchardCore.Users.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.Contents;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;
using OrchardCore.Roles;
using OrchardCore.Security;
using OrchardCore.Security.AuthorizationHandlers;
using OrchardCore.Security.Permissions;
using OrchardCore.Security.Services;
using OrchardCore.Settings;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using IUserService = OrchardCore.Users.Services.IUserService;

namespace CrestApps.OrchardCore.Tests.Modules.Users;

/// <summary>
/// SEC-C10 (review 11, C3): what a membership or a subscription bought in the store may grant. The tenant's
/// administrator role is never granted under whatever name the tenant gave it; with no allowlist, no role holding a
/// security-critical permission is granted; and a signed-in user who may not assign roles can't add one through the
/// content API or an import, which run no display driver. These use Orchard Core's own system role provider and
/// permission service.
/// </summary>
public sealed class RoleGrantPolicyTests
{
    // The tenant renamed its administrator role.
    private const string AdminRole = "Owners";

    private readonly User _user = new() { UserId = "u1", UserName = "ann" };

    [Fact]
    public async Task ARenamedAdministratorRole_IsNeverGranted()
    {
        // Arrange
        var service = ActivatorUtilities.CreateInstance<DefaultRoleGrantService>(CreateServices());

        // Act
        var granted = await service.GrantAsync("u1", AdminRole, "Membership", "m1");

        // Assert
        Assert.False(granted);
        Assert.DoesNotContain(AdminRole, _user.RoleNames);
    }

    [Fact]
    public async Task WithNoAllowlist_AMembershipDoesNotGrantARoleThatCanManageUsers()
    {
        // Arrange
        var grants = new Mock<IRoleGrantService>();
        var service = CreateMembershipService(grants, new MembershipSettings());

        // Act
        await service.GrantLineAsync(Order(), Line(), new MembershipPart { RoleNames = ["Staff", "Assigners", AdminRole, "Member"] }, TestContext.Current.CancellationToken);

        // Assert
        grants.Verify(grant => grant.GrantAsync("u1", "Member", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        grants.Verify(grant => grant.GrantAsync("u1", "Staff", It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        grants.Verify(grant => grant.GrantAsync("u1", "Assigners", It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        grants.Verify(grant => grant.GrantAsync("u1", AdminRole, It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ARoleTheStoresAllowlistNames_IsGranted_ButNeverTheAdministratorRole()
    {
        // Arrange
        var grants = new Mock<IRoleGrantService>();
        var service = CreateMembershipService(grants, new MembershipSettings { GrantableRoles = ["Staff", AdminRole] });

        // Act
        await service.GrantLineAsync(Order(), Line(), new MembershipPart { RoleNames = ["Staff", AdminRole] }, TestContext.Current.CancellationToken);

        // Assert
        grants.Verify(grant => grant.GrantAsync("u1", "Staff", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        grants.Verify(grant => grant.GrantAsync("u1", AdminRole, It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData("Staff", false)]
    [InlineData("Assigners", false)]
    [InlineData(AdminRole, false)]
    [InlineData("Member", true)]
    public async Task ASubscription_GrantsNoSecurityCriticalRole(string roleName, bool granted)
    {
        // Arrange
        var grants = new Mock<IRoleGrantService>();
        var applier = ActivatorUtilities.CreateInstance<RoleSubscriptionEntitlementApplier>(CreateServices(grants.Object));
        var context = new SubscriptionEntitlementContext(
            new Subscription { ItemId = "s1", OwnerId = "u1" },
            new SubscriptionEntitlement { Kind = SubscriptionConstants.EntitlementKinds.Role, Value = roleName });

        // Act
        await applier.ApplyAsync(context);

        // Assert
        grants.Verify(grant => grant.GrantAsync("u1", roleName, It.IsAny<string>(), It.IsAny<string>()), granted ? Times.Once() : Times.Never());
    }

    [Fact]
    public async Task TheContentApi_RefusesAProductEditorAddingARole()
    {
        // Arrange: Orchard Core's content API loads the stored product, merges what was posted, then validates.
        var editor = CalendarTestHarness.User("editor", CommonPermissions.EditContent.Name, MembershipsPermissions.ManageMemberships.Name);
        var handler = CreateMembershipHandler(editor);
        var stored = Product(["Member"]);

        await handler.LoadedAsync(new LoadContentContext(stored), stored.GetOrCreate<MembershipPart>());
        stored.Alter<MembershipPart>(part => part.RoleNames = ["Member", "Staff"]);

        // Act
        var context = new ValidateContentContext(stored);
        await handler.ValidatingAsync(context, stored.GetOrCreate<MembershipPart>());

        // Assert
        Assert.False(context.ContentValidateResult.Succeeded);
    }

    [Fact]
    public async Task TheContentApi_LetsAProductEditorKeepTheStoredRoles_AndAnAssignerAddOne()
    {
        // Arrange
        var editor = CalendarTestHarness.User("editor", CommonPermissions.EditContent.Name, MembershipsPermissions.ManageMemberships.Name);
        var assigner = CalendarTestHarness.User("assigner", MembershipsPermissions.ManageMemberships.Name, UsersPermissions.AssignRoleToUsers.Name);

        // Act
        var kept = await ValidateMembershipSaveAsync(editor, ["Member"], ["Member"]);
        var added = await ValidateMembershipSaveAsync(assigner, ["Member"], ["Member", "Gold"]);

        // Assert
        Assert.True(kept.Succeeded);
        Assert.True(added.Succeeded);
    }

    [Fact]
    public async Task ANewProductWithRoles_ByAnEditorWhoCantAssignThem_IsRefused_AsAnImportedOneIs()
    {
        // Arrange: nothing stored, as for a new item through the content API or an import.
        var editor = CalendarTestHarness.User("editor", CommonPermissions.EditContent.Name, MembershipsPermissions.ManageMemberships.Name);
        var item = Product(["Gold"]);
        var handler = CreateMembershipHandler(editor);

        // Act
        var context = new ValidateContentContext(item);
        await handler.ValidatingAsync(context, item.GetOrCreate<MembershipPart>());

        // Assert
        Assert.False(context.ContentValidateResult.Succeeded);
    }

    [Fact]
    public async Task ASubscriptionPlan_SavedThroughTheContentApi_ByAnEditorWhoCantAssignRoles_IsRefused()
    {
        // Arrange
        var editor = CalendarTestHarness.User("editor", CommonPermissions.EditContent.Name);
        var plan = new ContentItem { ContentItemId = "plan", ContentType = "Plan" };
        plan.Weld(new SubscriptionEntitlementPart { RoleNames = ["Member"] });
        var handler = new SubscriptionEntitlementPartRolesHandler(Accessor(editor), CreateAuthorization(), new PassThroughStringLocalizer<SubscriptionEntitlementPartRolesHandler>());

        await handler.LoadedAsync(new LoadContentContext(plan), plan.GetOrCreate<SubscriptionEntitlementPart>());
        plan.Alter<SubscriptionEntitlementPart>(part => part.RoleNames = ["Member", "Staff"]);

        // Act
        var context = new ValidateContentContext(plan);
        await handler.ValidatingAsync(context, plan.GetOrCreate<SubscriptionEntitlementPart>());

        // Assert
        Assert.False(context.ContentValidateResult.Succeeded);
    }

    [Fact]
    public async Task AnAuditTrailRestore_ThatBringsBackARemovedRole_NeedsTheRightToAssignRoles()
    {
        // Round 2 (Mild): a Restore loads the old version and the current one, and the roles of every loaded version
        // counted as already granted, so restoring an old version brought back a removed role without the right to assign it.
        var editor = CalendarTestHarness.User("editor", CommonPermissions.EditContent.Name, MembershipsPermissions.ManageMemberships.Name);
        var handler = CreateMembershipHandler(editor);
        var snapshot = Product(["Member", "Staff"]);
        var current = Product(["Member"]);

        await handler.LoadedAsync(new LoadContentContext(snapshot), snapshot.GetOrCreate<MembershipPart>());
        await handler.LoadedAsync(new LoadContentContext(current), current.GetOrCreate<MembershipPart>());

        // Act
        var context = new ValidateContentContext(snapshot);
        await handler.ValidatingAsync(context, snapshot.GetOrCreate<MembershipPart>());

        // Assert
        Assert.False(context.ContentValidateResult.Succeeded);
    }

    [Fact]
    public async Task ASaveWithNoSignedInUser_IsTrusted()
    {
        // Arrange: setup, a background task or a remote deployment.
        var item = Product(["Gold"]);
        var handler = new MembershipPartRolesHandler(new HttpContextAccessor(), CreateAuthorization(), new PassThroughStringLocalizer<MembershipPartRolesHandler>());

        // Act
        var context = new ValidateContentContext(item);
        await handler.ValidatingAsync(context, item.GetOrCreate<MembershipPart>());

        // Assert
        Assert.True(context.ContentValidateResult.Succeeded);
    }

    private static async Task<ContentValidateResult> ValidateMembershipSaveAsync(ClaimsPrincipal user, string[] storedRoles, string[] savedRoles)
    {
        var handler = CreateMembershipHandler(user);
        var item = Product(storedRoles);

        await handler.LoadedAsync(new LoadContentContext(item), item.GetOrCreate<MembershipPart>());
        item.Alter<MembershipPart>(part => part.RoleNames = savedRoles);

        var context = new ValidateContentContext(item);
        await handler.ValidatingAsync(context, item.GetOrCreate<MembershipPart>());

        return context.ContentValidateResult;
    }

    private static MembershipPartRolesHandler CreateMembershipHandler(ClaimsPrincipal user)
        => new(Accessor(user), CreateAuthorization(), new PassThroughStringLocalizer<MembershipPartRolesHandler>());

    private static HttpContextAccessor Accessor(ClaimsPrincipal user)
        => new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = user } };

    private static ContentItem Product(string[] roles)
    {
        var item = new ContentItem { ContentItemId = "product", ContentType = "Product" };
        item.Weld(new MembershipPart { RoleNames = roles, DurationDays = 30 });

        return item;
    }

    // The real authorization service with Orchard Core's permission handler.
    private static IAuthorizationService CreateAuthorization()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddAuthorizationCore();
        services.AddSingleton<IPermissionGrantingService, DefaultPermissionGrantingService>();
        services.AddScoped<IAuthorizationHandler, PermissionHandler>();

        return services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<IAuthorizationService>();
    }

    private MembershipService CreateMembershipService(Mock<IRoleGrantService> grants, MembershipSettings settings)
    {
        grants.Setup(grant => grant.GrantAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

        return ActivatorUtilities.CreateInstance<MembershipService>(CreateServices(grants.Object, settings));
    }

    // Orchard Core's system role provider for a tenant whose administrator role is "Owners", its permission service over
    // a provider with the Users permissions, and the roles: Staff holds ManageUsers; Assigners holds the permission to
    // assign one role, made for that role and not listed by any provider; Member and Gold hold a content permission.
    private ServiceProvider CreateServices(IRoleGrantService grants = null, MembershipSettings settings = null)
    {
        var roles = new Mock<IRoleService>();
        roles.Setup(service => service.GetRoleClaimsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string role, CancellationToken _) => role switch
            {
                "Staff" => [new Claim(Permission.ClaimType, UsersPermissions.ManageUsers.Name), new Claim(Permission.ClaimType, CommonPermissions.ViewContent.Name)],
                "Assigners" => [new Claim(Permission.ClaimType, UsersPermissions.AssignRoleToUsers.Name + "_Member")],
                _ => [new Claim(Permission.ClaimType, CommonPermissions.ViewContent.Name)],
            });

        var provider = new Mock<IPermissionProvider>();
        provider.Setup(p => p.GetPermissionsAsync()).ReturnsAsync([UsersPermissions.ManageUsers, UsersPermissions.AssignRoleToUsers, CommonPermissions.ViewContent]);

        var userManager = new Mock<UserManager<IUser>>(Mock.Of<IUserStore<IUser>>(), null, null, null, null, null, null, null, null);
        userManager.Setup(manager => manager.IsInRoleAsync(It.IsAny<IUser>(), It.IsAny<string>()))
            .ReturnsAsync((IUser user, string role) => ((User)user).RoleNames.Contains(role, StringComparer.OrdinalIgnoreCase));
        userManager.Setup(manager => manager.AddToRoleAsync(It.IsAny<IUser>(), It.IsAny<string>()))
            .ReturnsAsync((IUser user, string role) =>
            {
                ((User)user).RoleNames.Add(role);

                return IdentityResult.Success;
            });
        userManager.Setup(manager => manager.UpdateAsync(It.IsAny<IUser>())).ReturnsAsync(IdentityResult.Success);

        var users = new Mock<IUserService>();
        users.Setup(service => service.GetUserByUniqueIdAsync("u1")).ReturnsAsync(_user);

        var site = new SiteSettings();
        site.Put(settings ?? new MembershipSettings());
        var siteService = new Mock<ISiteService>();
        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site);

        var memberships = new Mock<IMembershipStore>();
        memberships.Setup(store => store.ListByUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddSingleton(new ShellSettings { Name = "Default" })
            .AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>))
            .AddSingleton(Options.Create(new SystemRoleOptions { SystemAdminRoleName = AdminRole }))
            .AddSingleton<ISystemRoleProvider, DefaultSystemRoleProvider>()
            .AddSingleton(roles.Object)
            .AddSingleton(provider.Object)
            .AddSingleton<IPermissionService, DefaultPermissionService>()
            .AddSingleton(userManager.Object)
            .AddSingleton(users.Object)
            .AddSingleton(siteService.Object)
            .AddSingleton(memberships.Object)
            .AddSingleton(Mock.Of<IClock>(clock => clock.UtcNow == new DateTime(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc)))
            .AddSingleton<IEnumerable<IRoleGrantSource>>([]);

        services.AddRoleGrants();

        if (grants is not null)
        {
            services.AddSingleton(grants);
        }

        return services.BuildServiceProvider();
    }

    private static Order Order()
        => new() { ItemId = "order-1", UserId = "u1" };

    private static OrderLine Line()
        => new() { LineId = "line-1", ContentItemId = "plan", Title = "Plan", Quantity = 1 };
}

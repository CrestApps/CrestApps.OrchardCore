using CrestApps.OrchardCore.Memberships.Models;
using CrestApps.OrchardCore.Memberships.Services;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Users;
using CrestApps.OrchardCore.Users.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using OrchardCore.Users.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Users;

/// <summary>
/// A role is granted by source and taken away only when no source still grants it, and never when the user was in it
/// before any source granted it.
/// </summary>
public sealed class RoleGrantServiceTests
{
    private static readonly DateTime _now = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

    private readonly User _user = new() { UserId = "u1", UserName = "ann" };
    private int _securityStampUpdates;

    [Fact]
    public async Task ASubscriptionEnding_DoesNotRemoveARole_AnActiveMembershipStillGrants()
    {
        // Arrange
        var service = CreateService();
        var applier = new RoleSubscriptionEntitlementApplier(service);
        var subscription = new Subscription { ItemId = "s1", OwnerId = "u1" };
        var context = new SubscriptionEntitlementContext(subscription, new SubscriptionEntitlement { Kind = SubscriptionConstants.EntitlementKinds.Role, Value = "Gold" });

        await service.GrantAsync("u1", "Gold", "Membership", "m1");
        await applier.ApplyAsync(context);

        // Act
        await applier.RevokeAsync(context);

        // Assert
        Assert.Contains("Gold", _user.RoleNames);
        Assert.Equal(0, _securityStampUpdates);

        Assert.True(await service.RevokeAsync("u1", "Gold", "Membership", "m1"));
        Assert.DoesNotContain("Gold", _user.RoleNames);
        Assert.Equal(1, _securityStampUpdates);
    }

    [Fact]
    public async Task ARoleTheUserWasInBeforeAnyGrant_IsKeptWhenTheGrantEnds()
    {
        _user.RoleNames.Add("Gold");

        var service = CreateService();

        Assert.True(await service.GrantAsync("u1", "Gold", "Subscription", "s1"));
        Assert.False(await service.RevokeAsync("u1", "Gold", "Subscription", "s1"));
        Assert.Contains("Gold", _user.RoleNames);
    }

    [Fact]
    public async Task GrantingTheSameRoleFromTheSameSourceTwice_RecordsItOnce()
    {
        var service = CreateService();

        await service.GrantAsync("u1", "Gold", "Membership", "m1");
        await service.GrantAsync("u1", "Gold", "Membership", "m1");

        var grant = Assert.Single(await service.ListAsync("u1"));

        Assert.Equal(("Gold", "Membership", "m1", _now), (grant.RoleName, grant.Source, grant.SourceId, grant.GrantedUtc));
        Assert.Single(_user.RoleNames, role => role == "Gold");
    }

    [Fact]
    public async Task AMembershipFromBeforeGrantsWereRecorded_StillKeepsItsRole_WhenASubscriptionEnds()
    {
        // Arrange: the membership put the user in Gold before grants were recorded by source.
        _user.RoleNames.Add("Gold");

        var memberships = new Mock<IMembershipStore>();
        memberships.Setup(store => store.ListByUserAsync("u1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Membership { ItemId = "m1", UserId = "u1", Status = MembershipStatus.Active, RoleNames = ["Gold"], AddedRoleNames = ["Gold"] }]);

        var service = CreateService(new MembershipRoleGrantSource(memberships.Object));
        var applier = new RoleSubscriptionEntitlementApplier(service);
        var context = new SubscriptionEntitlementContext(
            new Subscription { ItemId = "s1", OwnerId = "u1" },
            new SubscriptionEntitlement { Kind = SubscriptionConstants.EntitlementKinds.Role, Value = "Gold" });

        // Act
        await applier.RevokeAsync(context);

        // Assert: the membership's grant is recorded and keeps the role; ending the membership then removes it.
        Assert.Contains("Gold", _user.RoleNames);
        Assert.Equal(MembershipRoleGrantSource.SourceName, Assert.Single(await service.ListAsync("u1")).Source);
        Assert.True(await service.RevokeAsync("u1", "Gold", MembershipRoleGrantSource.SourceName, "m1"));
        Assert.DoesNotContain("Gold", _user.RoleNames);
    }

    [Fact]
    public async Task ASubscriptionFromBeforeGrantsWereRecorded_StillRemovesItsRole_WhenNothingElseGrantsIt()
    {
        // The subscription put the user in Gold before grants were recorded, and it's no longer current.
        _user.RoleNames.Add("Gold");

        var service = CreateService();
        var applier = new RoleSubscriptionEntitlementApplier(service);
        var context = new SubscriptionEntitlementContext(
            new Subscription { ItemId = "s1", OwnerId = "u1" },
            new SubscriptionEntitlement { Kind = SubscriptionConstants.EntitlementKinds.Role, Value = "Gold" });

        await applier.RevokeAsync(context);

        Assert.DoesNotContain("Gold", _user.RoleNames);
        Assert.Empty(await service.ListAsync("u1"));
    }

    private DefaultRoleGrantService CreateService(params IRoleGrantSource[] sources)
    {
        var userManager = new Mock<UserManager<IUser>>(Mock.Of<IUserStore<IUser>>(), null, null, null, null, null, null, null, null);
        userManager.Setup(manager => manager.IsInRoleAsync(It.IsAny<IUser>(), It.IsAny<string>()))
            .ReturnsAsync((IUser user, string role) => ((User)user).RoleNames.Contains(role, StringComparer.OrdinalIgnoreCase));
        userManager.Setup(manager => manager.AddToRoleAsync(It.IsAny<IUser>(), It.IsAny<string>()))
            .ReturnsAsync((IUser user, string role) =>
            {
                ((User)user).RoleNames.Add(role);

                return IdentityResult.Success;
            });
        userManager.Setup(manager => manager.RemoveFromRoleAsync(It.IsAny<IUser>(), It.IsAny<string>()))
            .ReturnsAsync((IUser user, string role) =>
            {
                var roleNames = ((User)user).RoleNames;

                foreach (var name in roleNames.Where(name => string.Equals(name, role, StringComparison.OrdinalIgnoreCase)).ToList())
                {
                    roleNames.Remove(name);
                }

                return IdentityResult.Success;
            });
        userManager.Setup(manager => manager.UpdateAsync(It.IsAny<IUser>())).ReturnsAsync(IdentityResult.Success);
        userManager.Setup(manager => manager.UpdateSecurityStampAsync(It.IsAny<IUser>()))
            .ReturnsAsync(() =>
            {
                _securityStampUpdates++;

                return IdentityResult.Success;
            });

        var userService = new Mock<IUserService>();
        userService.Setup(service => service.GetUserByUniqueIdAsync("u1")).ReturnsAsync(_user);

        var clock = new Mock<IClock>();
        clock.Setup(value => value.UtcNow).Returns(_now);

        return new DefaultRoleGrantService(userManager.Object, userService.Object, sources, clock.Object, NullLogger<DefaultRoleGrantService>.Instance);
    }
}

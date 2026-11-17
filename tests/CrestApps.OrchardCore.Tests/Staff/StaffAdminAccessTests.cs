using System.Security.Claims;
using CrestApps.OrchardCore.Staff;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using Microsoft.AspNetCore.Authorization;
using Moq;
using OrchardCore.Admin;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Staff;

/// <summary>
/// A staff member may open the admin to reach their own pages (My Profile, and My Pay and My bank accounts linked from
/// it), without any other module giving admin access.
/// </summary>
public sealed class StaffAdminAccessTests
{
    [Theory]
    [InlineData("linked", StaffStatus.Active, true, true)]
    [InlineData("linked", StaffStatus.Terminated, true, false)]
    [InlineData("linked", StaffStatus.Active, false, false)]
    [InlineData("stranger", StaffStatus.Active, true, false)]
    public async Task AdminAccess_IsGivenToALinkedStaffMemberWhoMayViewTheirOwnProfile(string userId, StaffStatus status, bool mayViewOwnProfile, bool expected)
    {
        var context = await HandleAsync(userId, status, mayViewOwnProfile, AdminPermissions.AccessAdminPanel);

        Assert.Equal(expected, context.HasSucceeded);
    }

    [Fact]
    public async Task OtherPermissions_AreNotGiven()
    {
        var context = await HandleAsync("linked", StaffStatus.Active, true, StaffPermissions.ManageStaff);

        Assert.False(context.HasSucceeded);
    }

    private static async Task<AuthorizationHandlerContext> HandleAsync(string userId, StaffStatus status, bool mayViewOwnProfile, Permission permission)
    {
        var staff = new Mock<IStaffMemberManager>();
        var authorization = new Mock<IAuthorizationService>();
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "Test"));

        staff.Setup(manager => manager.FindByUserIdAsync("linked", It.IsAny<CancellationToken>())).ReturnsAsync(new StaffMember { ItemId = "lee", UserId = "linked", Status = status });
        authorization
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.Is<IEnumerable<IAuthorizationRequirement>>(requirements => requirements.OfType<PermissionRequirement>().Any(requirement => requirement.Permission.Name == StaffPermissions.ViewOwnStaffProfile.Name))))
            .ReturnsAsync(mayViewOwnProfile ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        var handler = new StaffAdminAccessAuthorizationHandler(staff.Object, new Lazy<IAuthorizationService>(authorization.Object));
        var context = new AuthorizationHandlerContext([new PermissionRequirement(permission)], user, resource: null);

        await handler.HandleAsync(context);

        return context;
    }
}

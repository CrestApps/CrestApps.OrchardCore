using CrestApps.OrchardCore.Users;
using CrestApps.OrchardCore.Users.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Tests.Modules.Users;

/// <summary>
/// Round 2 (Mild): code sign-in left out only the administrator role, so a holder of Manage users, Manage roles or Assign
/// roles to users could sign in with their mailbox alone. Every role that leads to the whole site is left out now.
/// </summary>
public sealed class EmailSignInEligibilityTests
{
    [Theory]
    [InlineData("UserManager", false)]
    [InlineData("Customer", true)]
    public async Task ARoleThatLeadsToTheWholeSite_KeepsItsHolderFromCodeSignIn(string role, bool allowed)
    {
        // Arrange
        var user = new User { UserId = "user-1", UserName = "sam", Email = "sam@example.com", IsEnabled = true };
        var userManager = new Mock<UserManager<IUser>>(Mock.Of<IUserStore<IUser>>(), null, null, null, null, null, null, null, null);
        userManager.Setup(manager => manager.IsLockedOutAsync(user)).ReturnsAsync(false);
        userManager.Setup(manager => manager.GetTwoFactorEnabledAsync(user)).ReturnsAsync(false);
        userManager.Setup(manager => manager.IsInRoleAsync(user, It.IsAny<string>())).ReturnsAsync(false);
        userManager.Setup(manager => manager.GetRolesAsync(user)).ReturnsAsync([role]);
        userManager.Setup(manager => manager.HasPasswordAsync(user)).ReturnsAsync(true);

        var policy = new Mock<IRoleGrantPolicy>();
        policy.Setup(p => p.IsSecurityCriticalAsync("UserManager", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        policy.Setup(p => p.IsSecurityCriticalAsync("Customer", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var eligibility = new EmailSignInEligibility(userManager.Object, EmailSignInHarness.SystemRoles(), Options.Create(new EmailSignInOptions()), policy.Object);

        // Act
        var allows = await eligibility.AllowsAsync(user);

        // Assert
        Assert.Equal(allowed, allows);
    }
}

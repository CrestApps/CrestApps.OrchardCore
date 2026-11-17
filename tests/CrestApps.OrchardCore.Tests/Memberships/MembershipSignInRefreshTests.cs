using System.Reflection;
using System.Security.Claims;
using CrestApps.OrchardCore.Memberships.Handlers;
using CrestApps.OrchardCore.Orders.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using IUserService = OrchardCore.Users.Services.IUserService;

namespace CrestApps.OrchardCore.Tests.Memberships;

/// <summary>
/// G1 of the fourth fix-verification review: signing a buyer in again after a membership is paid writes the account's
/// current security stamp into the cookie but keeps its other claims, among them the provider the session signed in
/// through. Done for a cookie older than the account's last credential change, that would make a stale session show a
/// login it never used, so the sign-in is refreshed only while the cookie's stamp is current.
/// </summary>
public sealed class MembershipSignInRefreshTests
{
    [Fact]
    public async Task APaidMembership_RefreshesTheSignIn_WhenTheCookiesStampIsCurrent()
    {
        var (handler, signInManager, user) = CreateHandler(stampIsCurrent: true);

        await GrantedAsync(handler, Order(user.UserId), 1);

        signInManager.Verify(manager => manager.RefreshSignInAsync(user), Times.Once);
    }

    [Fact]
    public async Task APaidMembership_LeavesAStaleCookieAlone()
    {
        var (handler, signInManager, user) = CreateHandler(stampIsCurrent: false);

        await GrantedAsync(handler, Order(user.UserId), 1);

        signInManager.Verify(manager => manager.RefreshSignInAsync(It.IsAny<IUser>()), Times.Never);
    }

    [Fact]
    public async Task AnotherAccountsOrder_DoesNotRefreshTheSignIn()
    {
        var (handler, signInManager, _) = CreateHandler(stampIsCurrent: true);

        await GrantedAsync(handler, Order("someone-else"), 1);

        signInManager.Verify(manager => manager.RefreshSignInAsync(It.IsAny<IUser>()), Times.Never);
    }

    private static Order Order(string userId)
        => new() { ItemId = "order-1", UserId = userId };

    private static Task GrantedAsync(MembershipOrderEventHandler handler, Order order, int granted)
    {
        var method = typeof(MembershipOrderEventHandler).GetMethod("GrantedAsync", BindingFlags.Instance | BindingFlags.NonPublic);

        return (Task)method.Invoke(handler, [order, granted, TestContext.Current.CancellationToken]);
    }

    private static (MembershipOrderEventHandler Handler, Mock<SignInManager<IUser>> SignInManager, User User) CreateHandler(bool stampIsCurrent)
    {
        var user = new User { UserId = "buyer-1", UserName = "buyer", SecurityStamp = "stamp-2" };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, user.UserId),
            new Claim(ClaimTypes.AuthenticationMethod, "Google"),
            new Claim("AspNet.Identity.SecurityStamp", stampIsCurrent ? "stamp-2" : "stamp-1"),
        ], "Identity.Application"));

        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } };
        var userManager = new Mock<UserManager<IUser>>(Mock.Of<IUserStore<IUser>>(), null, null, null, null, null, null, null, null);
        var signInManager = new Mock<SignInManager<IUser>>(
            userManager.Object,
            accessor,
            Mock.Of<IUserClaimsPrincipalFactory<IUser>>(),
            null,
            null,
            null,
            null);

        // Identity's own check: the principal's user, only when its stamp claim matches the account's.
        signInManager
            .Setup(manager => manager.ValidateSecurityStampAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ClaimsPrincipal p) => p.FindFirstValue("AspNet.Identity.SecurityStamp") == user.SecurityStamp ? user : null);

        var userService = new Mock<IUserService>();
        userService.Setup(service => service.GetUserByUniqueIdAsync(user.UserId)).ReturnsAsync(user);

        var handler = new MembershipOrderEventHandler(
            null,
            Mock.Of<IContentManager>(),
            accessor,
            signInManager.Object,
            userService.Object,
            new NullStringLocalizer<MembershipOrderEventHandler>());

        return (handler, signInManager, user);
    }

    private sealed class NullStringLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}

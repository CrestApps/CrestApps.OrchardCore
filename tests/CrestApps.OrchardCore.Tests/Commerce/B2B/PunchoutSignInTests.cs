using System.Security.Claims;
using CrestApps.OrchardCore.Accounts.B2B.Controllers;
using CrestApps.OrchardCore.Accounts.B2B.Models;
using CrestApps.OrchardCore.Accounts.B2B.Services;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Carts.Core.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.DisplayManagement;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Tests.Commerce.B2B;

/// <summary>
/// SEC-C13: a punchout signs its buyer in only when the account may sign them in at all (enabled, not locked out, able
/// to sign in, no access to the admin), takes the key in a POST unless the account opted in to link mode, and marks the
/// session as a punchout.
/// </summary>
public sealed class PunchoutSignInTests
{
    private const string HookUrl = "https://procurement.example.com/oci/hook";

    private readonly string _key;
    private readonly B2BAccount _account;
    private readonly User _user = new() { UserId = "buyer-1", UserName = "buyer", IsEnabled = true };
    private readonly Mock<UserManager<IUser>> _userManager;
    private readonly Mock<SignInManager<IUser>> _signInManager;
    private readonly Mock<IAuthorizationService> _authorization = new();

    public PunchoutSignInTests()
    {
        (_key, var hash) = PunchoutService.NewKey();

        _account = TestB2BAccounts.Create(
            configure: part => part.Punchout = new AccountPunchout { Enabled = true, KeyHash = hash, UserId = "buyer-1" },
            members: [new AccountMember { UserId = "buyer-1", UserName = "buyer", Email = "buyer@contoso.example" }]);

        _userManager = new Mock<UserManager<IUser>>(Mock.Of<IUserStore<IUser>>(), null, null, null, null, null, null, null, null);
        _userManager.Setup(manager => manager.FindByIdAsync("buyer-1")).ReturnsAsync(_user);
        _userManager.Setup(manager => manager.IsInRoleAsync(It.IsAny<IUser>(), It.IsAny<string>())).ReturnsAsync(false);
        _userManager.Setup(manager => manager.IsLockedOutAsync(It.IsAny<IUser>())).ReturnsAsync(false);

        _signInManager = new Mock<SignInManager<IUser>>(
            _userManager.Object,
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IUserClaimsPrincipalFactory<IUser>>(),
            null,
            null,
            null,
            null);
        _signInManager.Setup(manager => manager.CanSignInAsync(It.IsAny<IUser>())).ReturnsAsync(true);
        _signInManager.Setup(manager => manager.CreateUserPrincipalAsync(It.IsAny<IUser>())).ReturnsAsync(new ClaimsPrincipal(new ClaimsIdentity("Identity.Application")));

        _authorization
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Failed());
    }

    [Fact]
    public async Task Start_PostWithTheKey_SignsTheBuyerInAsAPunchout()
    {
        // Arrange
        var controller = CreateController(HttpMethods.Post);

        // Act
        var result = await controller.Start(_account.AccountId);

        // Assert
        Assert.IsType<RedirectResult>(result);
        _signInManager.Verify(
            manager => manager.SignInWithClaimsAsync(_user, false, It.Is<IEnumerable<Claim>>(claims => claims.Any(claim => claim.Type == PunchoutService.ClaimType && claim.Value == _account.AccountId))),
            Times.Once);
    }

    [Fact]
    public async Task Start_DisabledUser_IsUnauthorized()
    {
        // Arrange
        _user.IsEnabled = false;
        var controller = CreateController(HttpMethods.Post);

        // Act
        var result = await controller.Start(_account.AccountId);

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
        AssertNotSignedIn();
    }

    [Fact]
    public async Task Start_UserWhoCantSignIn_IsUnauthorized()
    {
        // Arrange
        _signInManager.Setup(manager => manager.CanSignInAsync(It.IsAny<IUser>())).ReturnsAsync(false);
        var controller = CreateController(HttpMethods.Post);

        // Act
        var result = await controller.Start(_account.AccountId);

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
        AssertNotSignedIn();
    }

    [Fact]
    public async Task Start_UserWithAdminAccess_IsRefused()
    {
        // Arrange
        _authorization
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());
        var controller = CreateController(HttpMethods.Post);

        // Act
        var result = await controller.Start(_account.AccountId);

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
        AssertNotSignedIn();
    }

    [Fact]
    public async Task Start_GetWithoutLinkMode_IsMethodNotAllowed()
    {
        // Arrange
        var controller = CreateController(HttpMethods.Get);

        // Act
        var result = await controller.Start(_account.AccountId);

        // Assert
        Assert.Equal(StatusCodes.Status405MethodNotAllowed, Assert.IsAssignableFrom<IStatusCodeActionResult>(result).StatusCode);
        AssertNotSignedIn();
    }

    [Fact]
    public async Task Start_GetWithLinkModeAllowed_SignsTheBuyerIn()
    {
        // Arrange
        _account.Part.Punchout.AllowLinkSignIn = true;
        var controller = CreateController(HttpMethods.Get);

        // Act
        var result = await controller.Start(_account.AccountId);

        // Assert
        Assert.IsType<RedirectResult>(result);
    }

    private void AssertNotSignedIn()
    {
        _signInManager.Verify(manager => manager.SignInAsync(It.IsAny<IUser>(), It.IsAny<bool>(), It.IsAny<string>()), Times.Never);
        _signInManager.Verify(manager => manager.SignInAsync(It.IsAny<IUser>(), It.IsAny<AuthenticationProperties>(), It.IsAny<string>()), Times.Never);
        _signInManager.Verify(manager => manager.SignInWithClaimsAsync(It.IsAny<IUser>(), It.IsAny<bool>(), It.IsAny<IEnumerable<Claim>>()), Times.Never);
        _signInManager.Verify(manager => manager.SignInWithClaimsAsync(It.IsAny<IUser>(), It.IsAny<AuthenticationProperties>(), It.IsAny<IEnumerable<Claim>>()), Times.Never);
    }

    private PunchoutController CreateController(string method)
    {
        var accounts = new Mock<IB2BAccountStore>();

        accounts.Setup(store => store.FindByIdAsync(_account.AccountId, It.IsAny<CancellationToken>())).ReturnsAsync(_account);

        var httpContext = new DefaultHttpContext();
        var accessor = new HttpContextAccessor { HttpContext = httpContext };

        httpContext.Request.Method = method;

        if (method == HttpMethods.Post)
        {
            httpContext.Request.ContentType = "application/x-www-form-urlencoded";
            httpContext.Request.Form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                ["PASSWORD"] = _key,
                ["HOOK_URL"] = HookUrl,
            });
        }
        else
        {
            httpContext.Request.QueryString = QueryString.Create(new Dictionary<string, string> { ["key"] = _key, ["HOOK_URL"] = HookUrl });
        }

        var services = new ServiceCollection()
            .AddSingleton(accounts.Object)
            .AddSingleton(new PunchoutService(new EphemeralDataProtectionProvider(), accessor))
            .AddSingleton(_userManager.Object)
            .AddSingleton(_signInManager.Object)
            .AddSingleton(_authorization.Object)
            .AddSingleton(Mock.Of<ICartService>())
            .AddSingleton(Mock.Of<IShapeFactory>())
            .AddSingleton<ILogger<PunchoutController>>(NullLogger<PunchoutController>.Instance)
            .BuildServiceProvider();

        httpContext.RequestServices = services;

        var controller = ActivatorUtilities.CreateInstance<PunchoutController>(services);
        var url = new Mock<IUrlHelper>();

        url.Setup(helper => helper.Content(It.IsAny<string>())).Returns("/");
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.Url = url.Object;

        return controller;
    }
}

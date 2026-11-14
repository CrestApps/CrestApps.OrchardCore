using System.Net;
using System.Reflection;
using CrestApps.OrchardCore.Core.Throttling;
using CrestApps.OrchardCore.Throttling;
using CrestApps.OrchardCore.Users;
using CrestApps.OrchardCore.Users.Controllers;
using CrestApps.OrchardCore.Users.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Email;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking;
using OrchardCore.Modules;
using OrchardCore.Settings;
using OrchardCore.Users;
using OrchardCore.Users.Events;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Tests.Modules.Users;

/// <summary>
/// SEC-P1: e-mail code sign-in through the controller that serves it, with Orchard's own login events in the shapes
/// its reCAPTCHA and e-mail confirmation handlers take, the real code store and the real attempt limiter.
/// </summary>
public sealed class EmailSignInControllerTests
{
    private const string Email = "jordan@example.test";

    private readonly IDistributedCache _cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
    private readonly LocalLock _lock = new(NullLogger<LocalLock>.Instance);
    private readonly Mock<UserManager<IUser>> _userManager;
    private readonly Mock<SignInManager<IUser>> _signInManager;
    private readonly User _user = new() { UserId = "user-1", UserName = "jordan", Email = Email, IsEnabled = true, EmailConfirmed = true };
    private readonly List<ILoginFormEvent> _loginFormEvents = [];
    private readonly RecordingLoginFormEvent _recorder = new();
    // The memory cache expires entries by the machine clock, so the store's clock starts at now.
    private DateTime _now = DateTime.UtcNow;

    public EmailSignInControllerTests()
    {
        _userManager = new Mock<UserManager<IUser>>(Mock.Of<IUserStore<IUser>>(), null, null, null, null, null, null, null, null);
        _userManager.Setup(manager => manager.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync(_user);
        _userManager.Setup(manager => manager.IsLockedOutAsync(It.IsAny<IUser>())).ReturnsAsync(false);
        _userManager.Setup(manager => manager.GetTwoFactorEnabledAsync(It.IsAny<IUser>())).ReturnsAsync(false);
        _userManager.Setup(manager => manager.IsInRoleAsync(It.IsAny<IUser>(), It.IsAny<string>())).ReturnsAsync(false);
        _userManager.Setup(manager => manager.AccessFailedAsync(It.IsAny<IUser>())).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(manager => manager.ResetAccessFailedCountAsync(It.IsAny<IUser>())).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(manager => manager.UpdateAsync(It.IsAny<IUser>())).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(manager => manager.HasPasswordAsync(It.IsAny<IUser>())).ReturnsAsync(true);
        _userManager.Setup(manager => manager.GetLoginsAsync(It.IsAny<IUser>())).ReturnsAsync([]);
        _userManager.Setup(manager => manager.UpdateSecurityStampAsync(It.IsAny<IUser>())).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(manager => manager.RemovePasswordAsync(It.IsAny<IUser>())).ReturnsAsync(IdentityResult.Success);

        _signInManager = new Mock<SignInManager<IUser>>(
            _userManager.Object,
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IUserClaimsPrincipalFactory<IUser>>(),
            null,
            null,
            null,
            null);
        _signInManager.Setup(manager => manager.SignInAsync(It.IsAny<IUser>(), It.IsAny<bool>(), It.IsAny<string>())).Returns(Task.CompletedTask);

        _loginFormEvents.Add(_recorder);
    }

    [Fact]
    public async Task WithReCaptchaOn_TheRightCode_StillSignsIn()
    {
        // Arrange: Orchard's reCAPTCHA login handler reports an error from LoggingInAsync when the form carries no
        // captcha response, and the code form has no captcha widget.
        _loginFormEvents.Add(new ReCaptchaLikeLoginFormEvent());
        var code = await CreateCodes().IssueAsync(Email, TestContext.Current.CancellationToken);
        var controller = CreateController();

        // Act
        var result = await controller.Code(new EmailSignInViewModel { Email = Email, Code = code });

        // Assert
        Assert.IsType<LocalRedirectResult>(result);
        _signInManager.Verify(manager => manager.SignInAsync(_user, false, null), Times.Once);
        Assert.True(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task AnUnconfirmedUser_IsSignedIn_AndConfirmed_AfterTheRightCode()
    {
        // Arrange: Orchard's e-mail confirmation handler refuses an unconfirmed address from ValidatingLoginAsync when
        // the site requires confirmed e-mails. The code itself proves the address.
        _user.EmailConfirmed = false;
        _loginFormEvents.Add(new EmailConfirmationLikeLoginFormEvent());
        var code = await CreateCodes().IssueAsync(Email, TestContext.Current.CancellationToken);

        // Act
        var result = await CreateController().Code(new EmailSignInViewModel { Email = Email, Code = code });

        // Assert
        Assert.IsType<LocalRedirectResult>(result);
        Assert.True(_user.EmailConfirmed);
        _signInManager.Verify(manager => manager.SignInAsync(_user, false, null), Times.Once);
    }

    [Fact]
    public async Task AGuess_WithNoCodeIssued_DoesNotCountTowardTheAccountsLockout()
    {
        // Arrange: no code was ever sent to the address, so nobody can lock the account by posting digits for it.
        var controller = CreateController();

        // Act
        for (var i = 0; i < 5; i++)
        {
            await controller.Code(new EmailSignInViewModel { Email = Email, Code = "123456" });
        }

        // Assert
        _userManager.Verify(manager => manager.AccessFailedAsync(It.IsAny<IUser>()), Times.Never);
        _signInManager.Verify(manager => manager.SignInAsync(It.IsAny<IUser>(), It.IsAny<bool>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task AWrongGuess_AgainstALiveCode_CountsTowardTheLockout_AndRaisesTheEvents()
    {
        // Arrange
        var code = await CreateCodes().IssueAsync(Email, TestContext.Current.CancellationToken);
        var wrong = code == "000000" ? "111111" : "000000";
        _userManager.SetupSequence(manager => manager.IsLockedOutAsync(It.IsAny<IUser>()))
            .ReturnsAsync(false)
            .ReturnsAsync(true);

        // Act
        var result = await CreateController().Code(new EmailSignInViewModel { Email = Email, Code = wrong });

        // Assert
        Assert.IsType<ViewResult>(result);
        _userManager.Verify(manager => manager.AccessFailedAsync(_user), Times.Once);
        Assert.Contains("IsLockedOut", _recorder.Raised);
        Assert.Contains("LoggingInFailed", _recorder.Raised);
        _signInManager.Verify(manager => manager.SignInAsync(It.IsAny<IUser>(), It.IsAny<bool>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task TheRightCode_RaisesTheLoginEvents_AndResetsTheFailures()
    {
        // Arrange
        var code = await CreateCodes().IssueAsync(Email, TestContext.Current.CancellationToken);

        // Act
        await CreateController().Code(new EmailSignInViewModel { Email = Email, Code = code });

        // Assert
        Assert.Equal(["ValidatingLogin", "LoggedIn"], _recorder.Raised);
        _userManager.Verify(manager => manager.ResetAccessFailedCountAsync(_user), Times.Once);
    }

    [Fact]
    public async Task AnAdministrator_IsRefused_AndNoCodeIsSent()
    {
        // Arrange
        _userManager.Setup(manager => manager.IsInRoleAsync(_user, "Administrator")).ReturnsAsync(true);
        var email = new Mock<IEmailService>();

        // Act
        await CreateController(email.Object).Index(new EmailSignInViewModel { Email = Email });
        var result = await CreateController().Code(new EmailSignInViewModel { Email = Email, Code = "123456" });

        // Assert
        email.Verify(service => service.SendAsync(It.IsAny<MailMessage>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.IsType<ViewResult>(result);
        _signInManager.Verify(manager => manager.SignInAsync(It.IsAny<IUser>(), It.IsAny<bool>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ANetworkOverItsSendLimit_SpendsNoneOfAnAddresssSends()
    {
        // Arrange: one network asks for codes for ten other addresses, reaching its limit.
        var codes = CreateCodes();

        for (var i = 0; i < 10; i++)
        {
            await codes.TryCountSendAsync($"buyer{i}@example.test", "203.0.113.7", TestContext.Current.CancellationToken);
        }

        // Act: the same network now asks for the victim's address three times, then the victim asks from home.
        for (var i = 0; i < 3; i++)
        {
            Assert.False(await codes.TryCountSendAsync(Email, "203.0.113.7", TestContext.Current.CancellationToken));
        }

        var victim = await codes.TryCountSendAsync(Email, "198.51.100.20", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(victim);
    }

    [Fact]
    public async Task ChecksFromOneNetwork_AreLimited()
    {
        // Arrange
        var controller = CreateController();
        var code = await CreateCodes().IssueAsync(Email, TestContext.Current.CancellationToken);

        // Act: thirty checks from one network use its allowance, so the right code from it is turned away.
        for (var i = 0; i < 30; i++)
        {
            await CreateCodes().TryCountVerifyAsync("203.0.113.7", TestContext.Current.CancellationToken);
        }

        var result = await controller.Code(new EmailSignInViewModel { Email = Email, Code = code });

        // Assert
        Assert.IsType<ViewResult>(result);
        _signInManager.Verify(manager => manager.SignInAsync(It.IsAny<IUser>(), It.IsAny<bool>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void TheController_IsPartOfTheEmailCodeSignInFeature_SoItsRoutesAreGoneWhenTheFeatureIsOff()
    {
        // Orchard only maps a module's controllers for the features they belong to.
        var feature = typeof(EmailSignInController).GetCustomAttribute<FeatureAttribute>();

        Assert.Equal(UsersConstants.Feature.EmailCodeSignIn, feature?.FeatureName);
    }

    private EmailSignInCodes CreateCodes()
    {
        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(() => _now);

        var options = new AttemptLimitOptions()
            .AddPolicy(EmailSignInCodes.SendNetworkPolicy, 10, TimeSpan.FromMinutes(15))
            .AddPolicy(EmailSignInCodes.SendEmailPolicy, 3, TimeSpan.FromMinutes(15))
            .AddPolicy(EmailSignInCodes.VerifyNetworkPolicy, 30, TimeSpan.FromMinutes(15));
        var limiter = new DistributedAttemptLimiter(_cache, _lock, new ShellSettings { Name = "Default" }, clock.Object, Options.Create(options), NullLogger<DistributedAttemptLimiter>.Instance);

        return new EmailSignInCodes(_cache, limiter, new ShellSettings { Name = "Default" }, clock.Object, _lock);
    }

    private EmailSignInController CreateController(IEmailService emailService = null)
    {
        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(() => _now);

        var site = new Mock<ISite>();
        site.SetupGet(s => s.SiteName).Returns("Test site");
        var siteService = new Mock<ISiteService>();
        siteService.Setup(s => s.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        // No test here sends a code through the controller, so the e-mail service is only ever checked for calls.
        var email = emailService ?? Mock.Of<IEmailService>();

        var controller = new EmailSignInController(
            _userManager.Object,
            _signInManager.Object,
            CreateCodes(),
            email,
            siteService.Object,
            _loginFormEvents,
            clock.Object,
            Mock.Of<global::OrchardCore.DisplayManagement.Notify.INotifier>(),
            Options.Create(new EmailSignInOptions()),
            NullLogger<EmailSignInController>.Instance,
            new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<EmailSignInController>(),
            Mock.Of<Microsoft.AspNetCore.Mvc.Localization.IHtmlLocalizer<EmailSignInController>>());

        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");

        var url = new Mock<IUrlHelper>();
        url.Setup(u => u.IsLocalUrl(It.IsAny<string>())).Returns(true);
        url.Setup(u => u.Content(It.IsAny<string>())).Returns("/");

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.Url = url.Object;

        return controller;
    }

    private sealed class RecordingLoginFormEvent : ILoginFormEvent
    {
        public List<string> Raised { get; } = [];

        public Task<IActionResult> LoggingInAsync() => Task.FromResult<IActionResult>(null);

        public Task LoggingInAsync(string userName, Action<string, string> reportError)
        {
            Raised.Add("LoggingIn");

            return Task.CompletedTask;
        }

        public Task LoggingInFailedAsync(string userName)
        {
            Raised.Add("LoggingInFailed");

            return Task.CompletedTask;
        }

        public Task LoggingInFailedAsync(IUser user)
        {
            Raised.Add("LoggingInFailed");

            return Task.CompletedTask;
        }

        public Task IsLockedOutAsync(IUser user)
        {
            Raised.Add("IsLockedOut");

            return Task.CompletedTask;
        }

        public Task<IActionResult> ValidatingLoginAsync(IUser user)
        {
            Raised.Add("ValidatingLogin");

            return Task.FromResult<IActionResult>(null);
        }

        public Task LoggedInAsync(IUser user)
        {
            Raised.Add("LoggedIn");

            return Task.CompletedTask;
        }
    }

    // What Orchard's reCAPTCHA login handler does on a form with no captcha response.
    private sealed class ReCaptchaLikeLoginFormEvent : ILoginFormEvent
    {
        public Task<IActionResult> LoggingInAsync() => Task.FromResult<IActionResult>(null);

        public Task LoggingInAsync(string userName, Action<string, string> reportError)
        {
            reportError("ReCaptcha", "Failed to validate captcha");

            return Task.CompletedTask;
        }

        public Task LoggingInFailedAsync(string userName) => Task.CompletedTask;

        public Task LoggingInFailedAsync(IUser user) => Task.CompletedTask;

        public Task IsLockedOutAsync(IUser user) => Task.CompletedTask;

        public Task<IActionResult> ValidatingLoginAsync(IUser user) => Task.FromResult<IActionResult>(null);

        public Task LoggedInAsync(IUser user) => Task.CompletedTask;
    }

    // What Orchard's e-mail confirmation login handler does when the site requires confirmed e-mails.
    private sealed class EmailConfirmationLikeLoginFormEvent : ILoginFormEvent
    {
        public Task<IActionResult> LoggingInAsync() => Task.FromResult<IActionResult>(null);

        public Task LoggingInAsync(string userName, Action<string, string> reportError) => Task.CompletedTask;

        public Task LoggingInFailedAsync(string userName) => Task.CompletedTask;

        public Task LoggingInFailedAsync(IUser user) => Task.CompletedTask;

        public Task IsLockedOutAsync(IUser user) => Task.CompletedTask;

        public Task<IActionResult> ValidatingLoginAsync(IUser user)
            => Task.FromResult<IActionResult>(user is User { EmailConfirmed: false } ? new RedirectToActionResult("ConfirmEmailSent", "EmailConfirmation", null) : null);

        public Task LoggedInAsync(IUser user) => Task.CompletedTask;
    }
}

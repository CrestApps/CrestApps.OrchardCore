using CrestApps.Core.AI.Copilot;
using CrestApps.Core.AI.Copilot.Models;
using CrestApps.Core.AI.Copilot.Services;
using CrestApps.OrchardCore.AI.Chat.Copilot.Controllers;
using CrestApps.OrchardCore.AI.Chat.Copilot.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using global::OrchardCore.Admin;
using global::OrchardCore.DisplayManagement.Notify;
using global::OrchardCore.Users;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat.Copilot;

/// <summary>
/// SEC-A10: the Copilot OAuth callback must prove that the user who returns is the user who started the flow, or an
/// attacker's GitHub code links the victim's account to the attacker's GitHub.
/// </summary>
public sealed class CopilotAuthControllerStateTests
{
    [Fact]
    public async Task OAuthCallback_WithoutTheStateCookie_Returns400AndStoresNoCredential()
    {
        var (controller, credentialStore, _) = CreateController(cookieValue: null);

        var result = await controller.OAuthCallback("attacker-code", "attacker-nonce", error: null);

        Assert.IsType<BadRequestResult>(result);
        credentialStore.Verify(store => store.SaveProtectedCredentialAsync(It.IsAny<string>(), It.IsAny<CopilotProtectedCredential>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OAuthCallback_WithAWrongNonce_Returns400AndStoresNoCredential()
    {
        var (controller, credentialStore, oauthService) = CreateController(cookieValue: null);

        // Start a flow for this browser, then come back with another nonce.
        var cookie = StartFlow(oauthService, controller.HttpContext, "/Admin/ai");
        controller.HttpContext.Request.Headers.Cookie = cookie;

        var result = await controller.OAuthCallback("attacker-code", "not-the-nonce", error: null);

        Assert.IsType<BadRequestResult>(result);
        credentialStore.Verify(store => store.SaveProtectedCredentialAsync(It.IsAny<string>(), It.IsAny<CopilotProtectedCredential>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OAuthCallback_WithAValidStateAndAnError_RedirectsToTheValidatedReturnUrl()
    {
        var (controller, _, oauthService) = CreateController(cookieValue: null);
        var context = new DefaultHttpContext();
        var authorizationUrl = oauthService.GetAuthorizationUrlFor(context, "/Admin/ai");
        var nonce = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri(authorizationUrl).Query)["state"].ToString();
        controller.HttpContext.Request.Headers.Cookie = context.Response.Headers.SetCookie.ToString().Split(';')[0];

        var result = await controller.OAuthCallback(code: null, nonce, error: "access_denied");

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/Admin/ai", redirect.Url);
    }

    private static string StartFlow(TestOAuthService oauthService, HttpContext target, string returnUrl)
    {
        var context = new DefaultHttpContext();
        oauthService.GetAuthorizationUrlFor(context, returnUrl);

        return context.Response.Headers.SetCookie.ToString().Split(';')[0];
    }

    private static (CopilotAuthController Controller, Mock<ICopilotCredentialStore> CredentialStore, TestOAuthService OAuthService) CreateController(string cookieValue)
    {
        var httpContext = new DefaultHttpContext();
        var accessor = new FixedHttpContextAccessor { HttpContext = httpContext };
        var credentialStore = new Mock<ICopilotCredentialStore>();
        var dataProtection = new EphemeralDataProtectionProvider();
        var options = Mock.Of<IOptionsMonitor<CopilotOptions>>(monitor => monitor.CurrentValue == new CopilotOptions { ClientId = "client", ClientSecret = "secret" });
        var oauthService = new TestOAuthService(
            new GitHubOAuthService(credentialStore.Object, dataProtection, options, Mock.Of<IHttpClientFactory>(), TimeProvider.System, NullLogger<GitHubOAuthService>.Instance, accessor),
            credentialStore.Object,
            dataProtection,
            options);

        var userManager = new Mock<UserManager<IUser>>(Mock.Of<IUserStore<IUser>>(), null, null, null, null, null, null, null, null);
        userManager.Setup(manager => manager.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>())).ReturnsAsync(Mock.Of<IUser>());
        userManager.Setup(manager => manager.GetUserIdAsync(It.IsAny<IUser>())).ReturnsAsync("victim");

        var htmlLocalizer = new Mock<IHtmlLocalizer<CopilotAuthController>>();
        htmlLocalizer.Setup(localizer => localizer[It.IsAny<string>()]).Returns((string name) => new LocalizedHtmlString(name, name));
        htmlLocalizer.Setup(localizer => localizer[It.IsAny<string>(), It.IsAny<object[]>()]).Returns((string name, object[] _) => new LocalizedHtmlString(name, name));

        var controller = new CopilotAuthController(
            oauthService.Service,
            userManager.Object,
            Mock.Of<INotifier>(),
            new CopilotCallbackUrlProvider(Mock.Of<global::OrchardCore.Settings.ISiteService>(), accessor, Mock.Of<LinkGenerator>(), NullLogger<CopilotCallbackUrlProvider>.Instance),
            htmlLocalizer.Object,
            NullLogger<CopilotAuthController>.Instance,
            Options.Create(new AdminOptions()))
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
        };

        var urlHelper = new Mock<IUrlHelper>();
        urlHelper.Setup(helper => helper.IsLocalUrl(It.IsAny<string>())).Returns((string url) => url?.StartsWith('/') == true && !url.StartsWith("//", StringComparison.Ordinal));
        controller.Url = urlHelper.Object;

        if (cookieValue is not null)
        {
            httpContext.Request.Headers.Cookie = cookieValue;
        }

        return (controller, credentialStore, oauthService);
    }

    private sealed class FixedHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext HttpContext { get; set; }
    }

    /// <summary>
    /// Issues a state cookie for another request than the controller's, as the authorize step does.
    /// </summary>
    private sealed class TestOAuthService
    {
        private readonly ICopilotCredentialStore _credentialStore;
        private readonly IDataProtectionProvider _dataProtection;
        private readonly IOptionsMonitor<CopilotOptions> _options;

        public TestOAuthService(GitHubOAuthService service, ICopilotCredentialStore credentialStore, IDataProtectionProvider dataProtection, IOptionsMonitor<CopilotOptions> options)
        {
            Service = service;
            _credentialStore = credentialStore;
            _dataProtection = dataProtection;
            _options = options;
        }

        public GitHubOAuthService Service { get; }

        public string GetAuthorizationUrlFor(HttpContext context, string returnUrl)
            => new GitHubOAuthService(_credentialStore, _dataProtection, _options, Mock.Of<IHttpClientFactory>(), TimeProvider.System, NullLogger<GitHubOAuthService>.Instance, new FixedHttpContextAccessor { HttpContext = context })
                .GetAuthorizationUrl("https://localhost/copilot/OAuthCallback", returnUrl);
    }
}

using System.Security.Claims;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Users;
using CrestApps.OrchardCore.Users.Core.Services;
using CrestApps.OrchardCore.Users.Filters;
using CrestApps.OrchardCore.Users.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using OrchardCore.Users.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Users;

/// <summary>
/// F3 (third fix-verification review): a code signs the address's owner in to an account whose password or external
/// login somebody else may hold, with nothing removed. Every page says so plainly until the owner confirms.
/// G4 (fourth fix-verification review): the warning was tied to the code session's <c>amr=otp</c>, which a later
/// sign-in (linking or removing a login) drops, so it is now decided on the account: its address isn't proved and it
/// has a credential the session doesn't show (with G1's security stamp check). These run the filter over Orchard Core's
/// user store and claims principal factory on SQLite, with the feature's claims provider and the real confirmation page
/// service; each session's claims are what a sign-in would put in its cookie.
/// </summary>
public sealed class EmailOwnershipNoticeFilterTests : IAsyncLifetime
{
    private const string Email = "pat@example.test";
    private const string ConfirmPath = "/sign-in/email/confirm";

    private EmailSignInHarness _harness;

    public async ValueTask InitializeAsync()
        => _harness = await EmailSignInHarness.CreateAsync(services => services.AddScoped<IUserClaimsProvider, EmailOwnershipClaimsProvider>());

    public async ValueTask DisposeAsync()
        => await _harness.DisposeAsync();

    [Fact]
    public async Task ACodeSession_OnAnAccountWithCredentialsNobodyProved_IsToldOnEveryPage()
    {
        // Arrange
        await CreateAccountAsync();
        var codeSession = await SessionAsync(new Claim("amr", "otp"));

        // Act: two pages.
        var first = await RenderPageAsync(codeSession);
        var second = await RenderPageAsync(codeSession);

        // Assert
        var notice = Assert.Single(first);
        Assert.Equal(NotifyType.Warning, notice.Type);
        Assert.Contains("password", notice.Message.Name, StringComparison.Ordinal);
        Assert.Contains(ConfirmPath, Render(notice.Message), StringComparison.Ordinal);
        Assert.Single(second);
    }

    // G4: the owner signed in with a code and linked their own login; Orchard Core signed the browser in again through
    // it, without amr=otp. The password somebody else may hold is still there.
    [Fact]
    public async Task AfterTheCodeSessionLinksALogin_ItIsStillTold()
    {
        await CreateAccountAsync();

        Assert.Single(await RenderPageAsync(await SessionAsync(new Claim(ClaimTypes.AuthenticationMethod, "GitHub"))));
    }

    // G4 with G1: the session's login was replaced after its cookie was issued, so the session shows nothing.
    [Fact]
    public async Task ASessionWhoseLoginWasReplacedSinceItSignedIn_IsTold()
    {
        // Arrange
        await CreateAccountAsync(withPassword: false);
        var session = await SessionAsync(new Claim(ClaimTypes.AuthenticationMethod, "GitHub"));

        await _harness.InRequestAsync(async services =>
        {
            var manager = services.GetRequiredService<UserManager<IUser>>();
            var account = await manager.FindByIdAsync("user-1");

            Assert.True((await manager.RemoveLoginAsync(account, "GitHub", "github-user-1")).Succeeded);
            Assert.True((await manager.AddLoginAsync(account, new UserLoginInfo("GitHub", "github-squatter", "GitHub"))).Succeeded);

            return account;
        });

        // Act and assert
        Assert.Single(await RenderPageAsync(session));
    }

    [Fact]
    public async Task APasswordSession_OnAnAccountWithOnlyItsPassword_IsNotTold()
    {
        await CreateAccountAsync(withLogin: false);

        Assert.Empty(await RenderPageAsync(await SessionAsync(new Claim("amr", "pwd"))));
    }

    [Fact]
    public async Task APasswordSession_OnAnAccountWithALoginItDoesntShow_IsTold()
    {
        await CreateAccountAsync();

        Assert.Single(await RenderPageAsync(await SessionAsync(new Claim("amr", "pwd"))));
    }

    [Fact]
    public async Task OnceTheAddressIsProved_NothingIsSaid()
    {
        var user = new User { UserId = "user-1", UserName = "pat", Email = Email, EmailConfirmed = true, IsEnabled = true };
        UserEmailOwnership.MarkVerified(user.Properties, Email, _harness.Now);
        await _harness.CreateUserAsync(user);

        Assert.Empty(await RenderPageAsync(await SessionAsync(new Claim("amr", "otp"))));
    }

    [Fact]
    public async Task AFormPost_IsNotTold()
    {
        await CreateAccountAsync();

        Assert.Empty(await RenderPageAsync(await SessionAsync(new Claim("amr", "otp")), HttpMethods.Post));
    }

    // What keeps the filter cheap: only a session of an account whose address isn't proved and that has a credential
    // carries the mark, and the filter reads the account for no other session.
    [Fact]
    public async Task OnlyASessionOfAnAccountWithCredentialsInDoubt_CarriesTheMark()
    {
        // Arrange
        await CreateAccountAsync();
        var proved = new User { UserId = "user-2", UserName = "sam", Email = "sam@example.test", EmailConfirmed = true, IsEnabled = true };
        UserEmailOwnership.MarkVerified(proved.Properties, proved.Email, _harness.Now);
        await _harness.CreateUserAsync(proved);

        // Act
        var inDoubt = await _harness.SignInClaimsAsync("user-1");
        var settled = await _harness.SignInClaimsAsync("user-2");

        // Assert
        Assert.Contains(inDoubt, claim => claim.Type == UserCredentialsProof.CredentialsInDoubtClaim);
        Assert.DoesNotContain(settled, claim => claim.Type == UserCredentialsProof.CredentialsInDoubtClaim);
    }

    private Task<string> CreateAccountAsync(bool withPassword = true, bool withLogin = true)
        => _harness.CreateUserAsync(new User { UserId = "user-1", UserName = "pat", Email = Email, EmailConfirmed = true, IsEnabled = true }, withPassword, withLogin);

    private async Task<ClaimsPrincipal> SessionAsync(params Claim[] signInClaims)
        => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1"), .. await _harness.SignInClaimsAsync("user-1", signInClaims)], IdentityConstants.ApplicationScheme));

    // One page request: the action renders a view, and the filter runs around it.
    private Task<List<Notice>> RenderPageAsync(ClaimsPrincipal principal, string method = "GET")
        => _harness.InRequestAsync(async services =>
        {
            var notices = new List<Notice>();
            var notifier = new Mock<INotifier>();
            notifier.Setup(n => n.AddAsync(It.IsAny<NotifyType>(), It.IsAny<LocalizedHtmlString>()))
                .Callback((NotifyType type, LocalizedHtmlString message) => notices.Add(new Notice(type, message)))
                .Returns(ValueTask.CompletedTask);

            var httpContext = new DefaultHttpContext { RequestServices = services, User = principal };
            httpContext.Request.Method = method;
            httpContext.Request.Path = "/account/orders";

            var links = new Mock<LinkGenerator>();
            links.Setup(l => l.GetPathByAddress(It.IsAny<HttpContext>(), It.IsAny<RouteValuesAddress>(), It.IsAny<RouteValueDictionary>(), It.IsAny<RouteValueDictionary>(), It.IsAny<PathString?>(), It.IsAny<FragmentString>(), It.IsAny<LinkOptions>()))
                .Returns(ConfirmPath);

            var manager = services.GetRequiredService<UserManager<IUser>>();
            var page = new EmailOwnershipConfirmationPage(
                EmailSignInHarness.CreateSiteService(),
                new EmailSignInEligibility(manager, Options.Create(new EmailSignInOptions())),
                links.Object,
                new HttpContextAccessor { HttpContext = httpContext });
            var filter = new EmailOwnershipNoticeFilter(manager, page, notifier.Object, new PassThroughHtmlLocalizer<EmailOwnershipNoticeFilter>());

            var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
            var executing = new ActionExecutingContext(actionContext, [], new Dictionary<string, object>(), controller: new object());

            await filter.OnActionExecutionAsync(executing, () => Task.FromResult(new ActionExecutedContext(actionContext, [], controller: new object()) { Result = new ViewResult() }));

            return notices;
        });

    // The notice as the page shows it, with its arguments.
    private static string Render(LocalizedHtmlString message)
    {
        using var writer = new StringWriter();
        message.WriteTo(writer, System.Text.Encodings.Web.HtmlEncoder.Default);

        return writer.ToString();
    }

    private sealed record Notice(NotifyType Type, LocalizedHtmlString Message);
}

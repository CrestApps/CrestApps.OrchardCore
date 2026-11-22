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
using OrchardCore.Users.Handlers;
using OrchardCore.Users.Models;
using OrchardCore.Users.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Users;

/// <summary>
/// F3 (third fix-verification review): a code signs the address's owner in to an account whose password or external
/// login somebody else may hold, with nothing removed. Every page says so plainly until the owner confirms.
/// G4 (fourth fix-verification review): the warning was tied to the code session's <c>amr=otp</c>, which a later
/// sign-in (linking or removing a login) drops. K3 (fifth): deciding it on the account alone warned every account with
/// both a password and an external login on every page. So every page warns a code session, and every session of an
/// account whose credentials changed in a code session (the owner linked or removed a login from it); everyone else is
/// told at sign-in only (<see cref="EmailOwnershipLoginFormEventTests"/>). These run the filter over Orchard Core's user
/// store and claims principal factory on SQLite, with the feature's claims provider and user event handler and the real
/// confirmation page service; each session's claims are what a sign-in would put in its cookie.
/// </summary>
public sealed class EmailOwnershipNoticeFilterTests : IAsyncLifetime
{
    private const string Email = "pat@example.test";
    private const string ConfirmPath = "/sign-in/email/confirm";

    private EmailSignInHarness _harness;

    public async ValueTask InitializeAsync()
        => _harness = await EmailSignInHarness.CreateAsync(services =>
        {
            services.AddScoped<IUserClaimsProvider, EmailOwnershipClaimsProvider>();
            services.AddScoped<IUserEventHandler, EmailOwnershipUserEventHandler>();
        });

    public async ValueTask DisposeAsync()
        => await _harness.DisposeAsync();

    [Fact]
    public async Task ACodeSession_OnAnAccountWithCredentialsNobodyProved_IsToldOnEveryPage()
    {
        // Arrange
        await CreateAccountAsync();
        var codeSession = await CodeSessionAsync();

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
    // it, without amr=otp. The password somebody else may hold is still there, and the link marked the account.
    [Fact]
    public async Task AfterTheCodeSessionLinksALogin_ItIsStillTold_AndSoIsEveryLaterSessionThroughIt()
    {
        // Arrange
        await CreateAccountAsync(withLogin: false);
        await LinkAsync(await CodeSessionAsync(), "github-owner");

        // Act
        var throughTheLink = await RenderPageAsync(await SessionAsync(new Claim(ClaimTypes.AuthenticationMethod, "GitHub")));

        // Assert
        Assert.Single(throughTheLink);
        Assert.True(await ChangedInCodeSessionAsync());
    }

    // G1's victim (review 8 G1 and G4): the owner linked their login from a code session; the squatter, who holds the
    // password, then replaced it with their own login of the same provider. The owner's cookie is older than that, so it
    // shows nothing, and the page still warns.
    [Fact]
    public async Task G1Victim_WhoseLinkedLoginWasReplaced_IsTold()
    {
        // Arrange
        await CreateAccountAsync(withLogin: false);
        await LinkAsync(await CodeSessionAsync(), "github-owner");
        var ownerSession = await SessionAsync(new Claim(ClaimTypes.AuthenticationMethod, "GitHub"));

        await _harness.InRequestAsync(async services =>
        {
            var manager = services.GetRequiredService<UserManager<IUser>>();
            var account = await manager.FindByIdAsync("user-1");

            Assert.True((await manager.RemoveLoginAsync(account, "GitHub", "github-owner")).Succeeded);
            Assert.True((await manager.AddLoginAsync(account, new UserLoginInfo("GitHub", "github-squatter", "GitHub"))).Succeeded);

            return account;
        });

        // Act and assert
        Assert.Single(await RenderPageAsync(ownerSession));
    }

    // Removing a login from a code session marks the account too: Orchard Core signs the browser in again after it.
    [Fact]
    public async Task ACodeSessionThatRemovesALogin_MarksTheAccount()
    {
        // Arrange
        await CreateAccountAsync();
        var codeSession = await CodeSessionAsync();

        // Act
        await _harness.InRequestAsync(async services =>
        {
            services.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext { User = codeSession };
            var manager = services.GetRequiredService<UserManager<IUser>>();

            return await manager.RemoveLoginAsync(await manager.FindByIdAsync("user-1"), "GitHub", "github-user-1");
        });

        // Assert
        Assert.True(await ChangedInCodeSessionAsync());
        // Orchard Core's RemoveLogin signs the browser in again with no method: it shows nothing, and the password is left.
        Assert.Single(await RenderPageAsync(await SessionAsync()));
    }

    // A passkey added from a code session marks the account like a linked login: ASP.NET Core Identity keeps the
    // security stamp when it adds one, and the passkey may be the owner's next to a password somebody else holds.
    [Fact]
    public async Task ACodeSessionThatAddsAPasskey_MarksTheAccount()
    {
        // Arrange
        await CreateAccountAsync(withLogin: false);

        // Act
        await _harness.AddPasskeyAsync("user-1", session: await CodeSessionAsync());

        // Assert
        Assert.True(await ChangedInCodeSessionAsync());
        // A later passkey session shows neither the password nor which passkey it used, so it is told.
        Assert.Single(await RenderPageAsync(await SessionAsync(new Claim("amr", "pop"))));
    }

    // Removing a passkey keeps the stamp too.
    [Fact]
    public async Task ACodeSessionThatRemovesAPasskey_MarksTheAccount()
    {
        // Arrange
        await CreateAccountAsync(withLogin: false);
        var credentialId = await _harness.AddPasskeyAsync("user-1");
        var codeSession = await CodeSessionAsync();

        // Act
        var removed = await _harness.RemovePasskeyAsync("user-1", credentialId, codeSession);

        // Assert
        Assert.True(removed.Succeeded);
        Assert.True(await ChangedInCodeSessionAsync());
    }

    // Orchard Core signs a passkey in with amr=mfa when the site requires user verification, the value a password session
    // with a second factor carries. On a marked account with a password, that session doesn't show the password.
    [Fact]
    public async Task APasskeySessionWithMfa_OnAMarkedAccountWithAPassword_IsTold()
    {
        // Arrange
        await CreateAccountAsync(withLogin: false);
        await _harness.AddPasskeyAsync("user-1", session: await CodeSessionAsync());

        // Act and assert
        Assert.Single(await RenderPageAsync(await SessionAsync(new Claim("amr", "mfa"))));
    }

    // An ordinary save in a code session (a profile edit) changes no credential, so it marks nothing.
    [Fact]
    public async Task ACodeSessionThatChangesNoCredential_DoesNotMarkTheAccount()
    {
        // Arrange
        await CreateAccountAsync();
        var codeSession = await CodeSessionAsync();

        // Act
        await _harness.InRequestAsync(async services =>
        {
            services.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext { User = codeSession };
            var manager = services.GetRequiredService<UserManager<IUser>>();
            var account = (User)await manager.FindByIdAsync("user-1");
            account.Properties["Nickname"] = "Pat";

            return await manager.UpdateAsync(account);
        });

        // Assert
        Assert.False(await ChangedInCodeSessionAsync());
        Assert.Equal(CredentialsResetCheck.None, await _harness.CredentialsResets.CheckAsync("user-1", DateTimeOffset.UtcNow.AddMinutes(-5), securityStamp: null, TestContext.Current.CancellationToken));
    }

    // A6 (review 10): a session signed in before the code session marked the account carries no claim, so the filter
    // never read the account for it until Identity's stamp check refreshed its claims, up to 30 minutes later. The mark
    // asks the sign-in cookie's validation to refresh every session issued before it at its next request.
    [Fact]
    public async Task MarkingTheAccount_RefreshesEverySessionSignedInBeforeIt()
    {
        // Arrange: a password session from earlier, then the code session links the owner's login.
        await CreateAccountAsync(withLogin: false);
        var earlier = DateTimeOffset.UtcNow.AddMinutes(-5);

        // Act
        await LinkAsync(await CodeSessionAsync(), "github-owner");

        // Assert: the earlier session is refreshed, which gives it the mark's claim, and so the warning.
        Assert.Equal(CredentialsResetCheck.Refresh, await _harness.CredentialsResets.CheckAsync("user-1", earlier, securityStamp: null, TestContext.Current.CancellationToken));
        Assert.Equal(CredentialsResetCheck.None, await _harness.CredentialsResets.CheckAsync("user-1", DateTimeOffset.UtcNow.AddMinutes(2), securityStamp: null, TestContext.Current.CancellationToken));
        var refreshed = await SessionAsync(new Claim("amr", "pwd"));
        Assert.Contains(refreshed.Claims, claim => claim.Type == UserCredentialsProof.CredentialsInDoubtClaim);
        Assert.Single(await RenderPageAsync(refreshed));
    }

    [Fact]
    public async Task APasswordSession_OnAnAccountWithOnlyItsPassword_IsNotTold()
    {
        await CreateAccountAsync(withLogin: false);

        Assert.Empty(await RenderPageAsync(await SessionAsync(new Claim("amr", "pwd"))));
    }

    // K3: a default "register with GitHub" account has a password and the login. Neither kind of session is told on every
    // page; they are told at sign-in.
    [Theory]
    [InlineData("amr", "pwd")]
    [InlineData(ClaimTypes.AuthenticationMethod, "GitHub")]
    public async Task ASession_OnAnAccountWithAPasswordAndALogin_IsNotToldOnEveryPage(string claimType, string claimValue)
    {
        await CreateAccountAsync();

        Assert.Empty(await RenderPageAsync(await SessionAsync(new Claim(claimType, claimValue))));
    }

    // K3: a session signed in before the upgrade carries the earlier claim, which Orchard Core's stamp refresh copies
    // over. Its account was never marked, so it isn't warned.
    [Fact]
    public async Task ASessionWithTheClaimFromBeforeTheUpgrade_OnAnUnmarkedAccount_IsNotTold()
    {
        await CreateAccountAsync();

        Assert.Empty(await RenderPageAsync(await SessionAsync(new Claim(ClaimTypes.AuthenticationMethod, "GitHub"), new Claim(UserCredentialsProof.CredentialsInDoubtClaim, "true"))));
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

        Assert.Empty(await RenderPageAsync(await CodeSessionAsync(), HttpMethods.Post));
    }

    // What keeps the filter cheap (K3): only a session of an account whose address isn't proved, that has a credential and
    // whose credentials changed in a code session carries the mark, and the filter reads the account for no other session
    // than those and code sessions.
    [Fact]
    public async Task OnlyASessionOfAnAccountWhoseCredentialsChangedInACodeSession_CarriesTheMark()
    {
        // Arrange
        await CreateAccountAsync();
        var marked = new User { UserId = "user-2", UserName = "sam", Email = "sam@example.test", EmailConfirmed = true, IsEnabled = true };
        UserCredentialsProof.MarkChangedInCodeSession(marked, _harness.Now);
        await _harness.CreateUserAsync(marked);

        // Act
        var unmarked = await _harness.SignInClaimsAsync("user-1");
        var changed = await _harness.SignInClaimsAsync("user-2");

        // Assert
        Assert.DoesNotContain(unmarked, claim => claim.Type == UserCredentialsProof.CredentialsInDoubtClaim);
        Assert.Contains(changed, claim => claim.Type == UserCredentialsProof.CredentialsInDoubtClaim);
    }

    private Task<string> CreateAccountAsync(bool withPassword = true, bool withLogin = true)
        => _harness.CreateUserAsync(new User { UserId = "user-1", UserName = "pat", Email = Email, EmailConfirmed = true, IsEnabled = true }, withPassword, withLogin);

    private async Task<ClaimsPrincipal> SessionAsync(params Claim[] signInClaims)
        => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1"), .. await _harness.SignInClaimsAsync("user-1", signInClaims)], IdentityConstants.ApplicationScheme));

    // The session the code page signs in: amr=otp, and the account's credentials as they were then.
    private async Task<ClaimsPrincipal> CodeSessionAsync()
    {
        var fingerprint = await _harness.InRequestAsync(async services =>
            UserCredentialsProof.CredentialsFingerprint((User)await services.GetRequiredService<UserManager<IUser>>().FindByIdAsync("user-1")));

        return await SessionAsync(new Claim("amr", "otp"), new Claim(UserCredentialsProof.CodeSessionCredentialsClaim, fingerprint));
    }

    // Orchard Core's LinkLoginCallback, in a request signed in as the given session.
    private Task<IdentityResult> LinkAsync(ClaimsPrincipal session, string providerKey)
        => _harness.InRequestAsync(async services =>
        {
            services.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext { User = session };
            var manager = services.GetRequiredService<UserManager<IUser>>();
            var result = await manager.AddLoginAsync(await manager.FindByIdAsync("user-1"), new UserLoginInfo("GitHub", providerKey, "GitHub"));

            Assert.True(result.Succeeded);

            return result;
        });

    private Task<bool> ChangedInCodeSessionAsync()
        => _harness.InRequestAsync(async services =>
            UserCredentialsProof.ChangedInCodeSession((User)await services.GetRequiredService<UserManager<IUser>>().FindByIdAsync("user-1")));

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
                new EmailSignInEligibility(manager, EmailSignInHarness.SystemRoles(), Options.Create(new EmailSignInOptions())),
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

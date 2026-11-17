using System.Security.Claims;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Users;
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

namespace CrestApps.OrchardCore.Tests.Modules.Users;

/// <summary>
/// F3 (third fix-verification review): a code signs the address's owner in to an account whose password or external
/// login somebody else may hold, with nothing removed, and the only notice was shown once, after the sign-in, and said
/// nothing about them. Every page now says it plainly to a session an e-mailed code began, until the owner confirms.
/// These run the filter over Orchard Core's user store on SQLite, with the real confirmation page service.
/// </summary>
public sealed class EmailOwnershipNoticeFilterTests : IAsyncLifetime
{
    private const string Email = "pat@example.test";
    private const string ConfirmPath = "/sign-in/email/confirm";

    private EmailSignInHarness _harness;

    public async ValueTask InitializeAsync()
        => _harness = await EmailSignInHarness.CreateAsync();

    public async ValueTask DisposeAsync()
        => await _harness.DisposeAsync();

    [Fact]
    public async Task ACodeSession_OnAnAccountWithCredentialsNobodyProved_IsToldOnEveryPage()
    {
        // Arrange
        await _harness.CreateUserAsync(new User { UserId = "user-1", UserName = "pat", Email = Email, EmailConfirmed = true, IsEnabled = true });
        var codeSession = SignedIn(new Claim("amr", "otp"));

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

    [Fact]
    public async Task APasswordSession_IsNotTold()
    {
        await _harness.CreateUserAsync(new User { UserId = "user-1", UserName = "pat", Email = Email, EmailConfirmed = true, IsEnabled = true });

        Assert.Empty(await RenderPageAsync(SignedIn(new Claim("amr", "pwd"))));
    }

    [Fact]
    public async Task OnceTheAddressIsProved_NothingIsSaid()
    {
        var user = new User { UserId = "user-1", UserName = "pat", Email = Email, EmailConfirmed = true, IsEnabled = true };
        UserEmailOwnership.MarkVerified(user.Properties, Email, _harness.Now);
        await _harness.CreateUserAsync(user);

        Assert.Empty(await RenderPageAsync(SignedIn(new Claim("amr", "otp"))));
    }

    [Fact]
    public async Task AFormPost_IsNotTold()
    {
        await _harness.CreateUserAsync(new User { UserId = "user-1", UserName = "pat", Email = Email, EmailConfirmed = true, IsEnabled = true });

        Assert.Empty(await RenderPageAsync(SignedIn(new Claim("amr", "otp")), HttpMethods.Post));
    }

    private static ClaimsPrincipal SignedIn(params Claim[] claims)
        => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1"), .. claims], "Identity.Application"));

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

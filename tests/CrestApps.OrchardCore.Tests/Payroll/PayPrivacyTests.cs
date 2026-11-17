using System.Reflection;
using System.Security.Claims;
using CrestApps.OrchardCore.Payroll.Controllers;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Users;
using CrestApps.OrchardCore.Users.Core.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Payroll;

/// <summary>
/// Pay is private: a page that shows pay statements or pay details asks the signed-in person for their password first, and
/// the confirmation holds for that person only, for a few minutes, so someone at a computer left signed in can't read it.
/// </summary>
public sealed class PayPrivacyTests
{
    [Fact]
    public async Task AConfirmation_HoldsForThePersonWhoConfirmed_NotForAnotherUserOrAChangedCookie()
    {
        // Arrange: the person confirms; the cookie the response sets comes back on the next request.
        var provider = new EphemeralDataProtectionProvider();
        var lee = User("lee-id");
        var confirming = new DefaultHttpContext();

        await Service(confirming, provider).ConfirmAsync(lee, TestContext.Current.CancellationToken);

        var cookie = Cookie(confirming);

        // Act
        var forLee = await Service(Request(cookie), provider).IsConfirmedAsync(lee, TestContext.Current.CancellationToken);
        var forSam = await Service(Request(cookie), provider).IsConfirmedAsync(User("sam-id"), TestContext.Current.CancellationToken);
        var changed = await Service(Request(cookie + "x"), provider).IsConfirmedAsync(lee, TestContext.Current.CancellationToken);
        var none = await Service(new DefaultHttpContext(), provider).IsConfirmedAsync(lee, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(forLee);
        Assert.False(forSam);
        Assert.False(changed);
        Assert.False(none);
        Assert.Equal(TimeSpan.FromMinutes(10), Service(confirming, provider).Window);
        Assert.Contains("httponly", confirming.Response.Headers.SetCookie.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", confirming.Response.Headers.SetCookie.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task APageThatNeedsTheConfirmation_SendsThePersonToConfirm_AndBackToThePage()
    {
        // Arrange
        var confirmation = new FakeConfirmation();
        var context = Executing(confirmation, HttpMethods.Get, "/Admin/my-pay/statements/7", "?print=1");
        var ran = false;

        // Act
        await new ConfirmPasswordAttribute().OnActionExecutionAsync(context, () =>
        {
            ran = true;

            return Task.FromResult<ActionExecutedContext>(null);
        });

        // Assert
        Assert.False(ran);
        Assert.Equal("/confirm?returnUrl=/Admin/my-pay/statements/7?print=1", Assert.IsType<RedirectResult>(context.Result).Url);
    }

    [Fact]
    public async Task AFormPostedAfterTheConfirmationRanOut_ComesBackToThePageItWasOn()
    {
        // Arrange
        var context = Executing(new FakeConfirmation(), HttpMethods.Post, "/Admin/payroll/pay-runs/statements/7/void", string.Empty);

        context.HttpContext.Request.Headers.Referer = "https://localhost/Admin/payroll/pay-runs/statements/7";

        // Act
        await new ConfirmPasswordAttribute().OnActionExecutionAsync(context, () => Task.FromResult<ActionExecutedContext>(null));

        // Assert
        Assert.Equal("/confirm?returnUrl=/Admin/payroll/pay-runs/statements/7", Assert.IsType<RedirectResult>(context.Result).Url);
    }

    [Fact]
    public async Task AConfirmedPerson_SeesThePage_AndWithoutTheServiceThePageIsRefused()
    {
        // Arrange
        var confirmed = Executing(new FakeConfirmation { Confirmed = true }, HttpMethods.Get, "/Admin/my-pay", string.Empty);
        var withoutService = Executing(null, HttpMethods.Get, "/Admin/my-pay", string.Empty);
        var ran = false;

        // Act
        await new ConfirmPasswordAttribute().OnActionExecutionAsync(confirmed, () =>
        {
            ran = true;

            return Task.FromResult<ActionExecutedContext>(null);
        });
        await new ConfirmPasswordAttribute().OnActionExecutionAsync(withoutService, () => Task.FromResult<ActionExecutedContext>(null));

        // Assert
        Assert.True(ran);
        Assert.Null(confirmed.Result);
        Assert.IsType<ForbidResult>(withoutService.Result);
    }

    [Fact]
    public async Task PayCardsOnAProfile_ShowOnePromptUntilConfirmed_ThenShow()
    {
        // Arrange
        var unconfirmed = Signed(new FakeConfirmation());
        var confirmed = Signed(new FakeConfirmation { Confirmed = true });

        // Act
        var first = await StaffPayPrivacy.DecideAsync(unconfirmed);
        var second = await StaffPayPrivacy.DecideAsync(unconfirmed);
        var shown = await StaffPayPrivacy.DecideAsync(confirmed);

        // Assert
        Assert.Equal(StaffPayVisibility.Prompt, first);
        Assert.Equal(StaffPayVisibility.Hide, second);
        Assert.Equal(StaffPayVisibility.Show, shown);
        Assert.Equal("/confirm?returnUrl=/Admin/staff/profile/1#staff-pay", StaffPayPrivacy.ConfirmUrl(unconfirmed));
    }

    [Fact]
    public async Task APersonsOwnPay_AsksEveryTimeTheProfileOpens_WhileSomeoneElsesHoldsForTheWindow()
    {
        // Arrange: Lee confirmed a few minutes ago, but not for this page.
        var own = new StaffMember { ItemId = "1", UserId = "lee-id" };
        var colleague = new StaffMember { ItemId = "2", UserId = "sam-id" };

        // Act
        var ownPay = await StaffPayPrivacy.DecideAsync(Signed(new FakeConfirmation { Confirmed = true }), own);
        var colleaguePay = await StaffPayPrivacy.DecideAsync(Signed(new FakeConfirmation { Confirmed = true }), colleague);
        var ownPayJustConfirmed = await StaffPayPrivacy.DecideAsync(Signed(new FakeConfirmation { Confirmed = true, JustConfirmed = true }), own);

        // Assert
        Assert.Equal(StaffPayVisibility.Prompt, ownPay);
        Assert.Equal(StaffPayVisibility.Show, colleaguePay);
        Assert.Equal(StaffPayVisibility.Show, ownPayJustConfirmed);
    }

    [Fact]
    public async Task AJustConfirmedPassword_ShowsTheNextPageOnce_ForThePersonWhoTypedIt()
    {
        // Arrange: Lee types the password; the next request carries both cookies the response set.
        var provider = new EphemeralDataProtectionProvider();
        var lee = User("lee-id");
        var confirming = new DefaultHttpContext();

        await Service(confirming, provider).ConfirmAsync(lee, TestContext.Current.CancellationToken);

        var cookies = Cookies(confirming);
        var page = RequestWith(cookies);
        var service = Service(page, provider);

        // Act: every pay card of the page asks, then the page opens again with the cookie used up.
        var first = await service.IsJustConfirmedAsync(lee, TestContext.Current.CancellationToken);
        var sameRequest = await service.IsJustConfirmedAsync(lee, TestContext.Current.CancellationToken);
        var forSam = await Service(RequestWith(cookies), provider).IsJustConfirmedAsync(User("sam-id"), TestContext.Current.CancellationToken);
        var openedAgain = await Service(RequestWith(cookies.Where(cookie => cookie.Key != PasswordConfirmation.JustConfirmedCookieName)), provider)
            .IsJustConfirmedAsync(lee, TestContext.Current.CancellationToken);
        var stillInWindow = await Service(RequestWith(cookies), provider).IsConfirmedAsync(lee, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(first);
        Assert.True(sameRequest);
        Assert.False(forSam);
        Assert.False(openedAgain);
        Assert.True(stillInWindow);
        Assert.Contains(PasswordConfirmation.JustConfirmedCookieName + "=;", page.Response.Headers.SetCookie.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void EveryPayrollPageShowingPayStatementsOrPayDetails_AsksForThePassword()
    {
        // The pages of a person's own pay, pay profiles, pay previews, pay items and garnishments ask for every action.
        foreach (var controller in new[] { typeof(MyPayController), typeof(PayProfilesController), typeof(PayPreviewsController), typeof(PayItemsController), typeof(GarnishmentsController) })
        {
            Assert.True(controller.IsDefined(typeof(ConfirmPasswordAttribute)), controller.Name);
        }

        // Pay runs ask on a run's statements, a statement and its stub, and the actions on a statement; the list doesn't.
        var payRuns = typeof(PayRunsController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        foreach (var action in new[] { "Details", "Statement", "StatementPdf", "VoidStatement", "ReissuePayment" })
        {
            Assert.All(payRuns.Where(method => method.Name == action), method => Assert.True(method.IsDefined(typeof(ConfirmPasswordAttribute)), action));
        }

        Assert.All(payRuns.Where(method => method.Name == "Index"), method => Assert.False(method.IsDefined(typeof(ConfirmPasswordAttribute))));
    }

    [Fact]
    public void ThePayPrompt_IsBuiltWithABlockBody_SoItBindsAsAnInitializerNotAnAsyncOne()
    {
        // A lambda like "shape => shape.ConfirmUrl = url" on a dynamic shape converts to Func<dynamic, ValueTask>: it
        // compiles, then fails when the profile renders and the Pay section shows nothing at all.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        var drivers = new[]
        {
            Path.Combine("src", "Modules", "CrestApps.OrchardCore.Staff", "Drivers", "StaffFeatureDrivers.cs"),
            Path.Combine("src", "Modules", "CrestApps.OrchardCore.Payroll", "Drivers", "StaffPayrollDisplayDriver.cs"),
            Path.Combine("src", "Modules", "CrestApps.OrchardCore.Payroll", "Drivers", "MyPayProfileDisplayDriver.cs"),
        };

        foreach (var driver in drivers)
        {
            var source = File.ReadAllText(Path.Combine(directory.FullName, driver));

            Assert.Contains("StaffPayPrivacy.PromptShape", source, StringComparison.Ordinal);
            Assert.DoesNotMatch(@"PromptShape,\s*shape\s*=>\s*shape\.", source);
        }
    }

    private static ClaimsPrincipal User(string id)
        => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id)], "Test"));

    private static PasswordConfirmation Service(DefaultHttpContext httpContext, IDataProtectionProvider provider)
        => new(new HttpContextAccessor { HttpContext = httpContext }, provider, Mock.Of<LinkGenerator>());

    private static string Cookie(DefaultHttpContext httpContext)
    {
        var header = httpContext.Response.Headers.SetCookie.ToString();
        var start = header.IndexOf('=') + 1;

        return Uri.UnescapeDataString(header[start..header.IndexOf(';')]);
    }

    private static Dictionary<string, string> Cookies(DefaultHttpContext httpContext)
        => httpContext.Response.Headers.SetCookie
            .Select(header => header[..header.IndexOf(';')])
            .ToDictionary(pair => pair[..pair.IndexOf('=')], pair => Uri.UnescapeDataString(pair[(pair.IndexOf('=') + 1)..]));

    private static DefaultHttpContext RequestWith(IEnumerable<KeyValuePair<string, string>> cookies)
    {
        var httpContext = new DefaultHttpContext();

        httpContext.Request.Headers.Cookie = string.Join("; ", cookies.Select(cookie => cookie.Key + "=" + Uri.EscapeDataString(cookie.Value)));

        return httpContext;
    }

    private static DefaultHttpContext Request(string cookie)
    {
        var httpContext = new DefaultHttpContext();

        httpContext.Request.Headers.Cookie = PasswordConfirmation.CookieName + "=" + Uri.EscapeDataString(cookie);

        return httpContext;
    }

    private static DefaultHttpContext Signed(IPasswordConfirmation confirmation)
    {
        var services = new ServiceCollection();

        if (confirmation is not null)
        {
            services.AddSingleton(confirmation);
        }

        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider(), User = User("lee-id") };

        httpContext.Request.Path = "/Admin/staff/profile/1";

        return httpContext;
    }

    private static ActionExecutingContext Executing(IPasswordConfirmation confirmation, string method, string path, string query)
    {
        var httpContext = Signed(confirmation);

        httpContext.Request.Method = method;
        httpContext.Request.Host = new HostString("localhost");
        httpContext.Request.Path = path;
        httpContext.Request.QueryString = new QueryString(query);

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());

        return new ActionExecutingContext(actionContext, [], new Dictionary<string, object>(), controller: null);
    }

    private sealed class FakeConfirmation : IPasswordConfirmation
    {
        public bool Confirmed { get; set; }

        public TimeSpan Window
            => TimeSpan.FromMinutes(10);

        public bool JustConfirmed { get; set; }

        public Task<bool> IsConfirmedAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
            => Task.FromResult(Confirmed);

        public Task<bool> IsJustConfirmedAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
            => Task.FromResult(JustConfirmed);

        public Task ConfirmAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
        {
            Confirmed = true;

            return Task.CompletedTask;
        }

        public string GetConfirmationUrl(string returnUrl)
            => "/confirm?returnUrl=" + returnUrl;
    }
}

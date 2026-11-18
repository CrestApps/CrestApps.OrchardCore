using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Users;
using CrestApps.OrchardCore.Users.Core.Services;
using CrestApps.OrchardCore.Users.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Tests.Modules.Users;

/// <summary>
/// K3 (fifth fix-verification review): an account whose address isn't proved and that has more than one way to sign in
/// is told so once, at sign-in, like the Customers feature's guest-order notice, not on every page. Sessions the filter
/// warns on every page are not told here too.
/// </summary>
public sealed class EmailOwnershipLoginFormEventTests
{
    private const string Email = "pat@example.test";
    private const string ConfirmPath = "/sign-in/email/confirm";

    [Fact]
    public async Task AnAccountWithAPasswordAndALogin_IsToldOnceAtSignIn()
    {
        var notices = await SignInAsync(Account(withPassword: true, logins: 1));

        var notice = Assert.Single(notices);
        Assert.Equal(NotifyType.Warning, notice.Type);
        Assert.Contains("confirm it's you", notice.Message.Name, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    public async Task AnAccountWithOneWayToSignIn_IsNotTold(bool withPassword, int logins)
        => Assert.Empty(await SignInAsync(Account(withPassword, logins)));

    [Fact]
    public async Task AnAccountWhoseAddressIsProved_IsNotTold()
    {
        var account = Account(withPassword: true, logins: 1);
        UserEmailOwnership.MarkVerified(account.Properties, Email, DateTime.UtcNow);

        Assert.Empty(await SignInAsync(account));
    }

    // Every page warns those, so the sign-in doesn't say it twice.
    [Fact]
    public async Task AnAccountWhoseCredentialsChangedInACodeSession_IsNotToldHereToo()
    {
        var account = Account(withPassword: true, logins: 1);
        UserCredentialsProof.MarkChangedInCodeSession(account, DateTime.UtcNow);

        Assert.Empty(await SignInAsync(account));
    }

    [Fact]
    public async Task ASignInWithACode_IsNotToldHereToo()
        => Assert.Empty(await SignInAsync(Account(withPassword: true, logins: 1), codeSignIn: true));

    private static User Account(bool withPassword, int logins)
    {
        var account = new User { UserId = "user-1", UserName = "pat", Email = Email, EmailConfirmed = true, IsEnabled = true, PasswordHash = withPassword ? "hash" : null };

        for (var i = 0; i < logins; i++)
        {
            account.LoginInfos.Add(new UserLoginInfo("GitHub" + i, "key-" + i, "GitHub"));
        }

        return account;
    }

    private static async Task<List<(NotifyType Type, LocalizedHtmlString Message)>> SignInAsync(User account, bool codeSignIn = false)
    {
        var notices = new List<(NotifyType Type, LocalizedHtmlString Message)>();
        var notifier = new Mock<INotifier>();
        notifier.Setup(n => n.AddAsync(It.IsAny<NotifyType>(), It.IsAny<LocalizedHtmlString>()))
            .Callback((NotifyType type, LocalizedHtmlString message) => notices.Add((type, message)))
            .Returns(ValueTask.CompletedTask);

        var page = new Mock<IEmailOwnershipConfirmationPage>();
        page.Setup(p => p.GetUrlAsync(It.IsAny<IUser>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(ConfirmPath);

        var httpContext = new DefaultHttpContext();

        if (codeSignIn)
        {
            EmailOwnershipLoginFormEvent.MarkCodeSignIn(httpContext);
        }

        var services = new ServiceCollection()
            .AddSingleton(notifier.Object)
            .AddSingleton(page.Object)
            .AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = httpContext })
            .AddSingleton<IHtmlLocalizer<EmailOwnershipLoginFormEvent>>(new PassThroughHtmlLocalizer<EmailOwnershipLoginFormEvent>())
            .BuildServiceProvider();

        await new EmailOwnershipLoginFormEvent(services, NullLogger<EmailOwnershipLoginFormEvent>.Instance).LoggedInAsync(account);

        return notices;
    }
}

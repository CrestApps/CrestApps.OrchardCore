using System.Security.Claims;
using CrestApps.OrchardCore.Users.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Security;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tests.Modules.Users;

/// <summary>
/// HT2 (fix-verification review): the first proof of an address rotated the security stamp "so other sessions end", but
/// ASP.NET Core Identity compares the stamp only every <see cref="SecurityStampValidatorOptions.ValidationInterval"/>,
/// 30 minutes by default, so whoever had signed in before kept reading the account, and the guest orders just merged
/// into it, until then. These authenticate a real sign-in cookie with the application cookie handler as
/// <c>AddIdentity</c> configures it, Orchard Core's own registration.
/// </summary>
public sealed class CredentialsResetCookieTests : IAsyncDisposable
{
    private const string UserId = "user-1";

    private readonly ServiceProvider _services;

    public CredentialsResetCookieTests()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        services.AddDistributedMemoryCache();
        services.AddSingleton(new ShellSettings { Name = "Default" });

        // What Orchard Core's Users feature registers: Identity, with its application cookie and security stamp check.
        services.AddIdentity<IUser, IRole>();
        services.AddSingleton(Mock.Of<IUserStore<IUser>>());
        services.AddSingleton(Mock.Of<IRoleStore<IRole>>());

        // What the Email Code Sign-In feature registers.
        services.AddSingleton<UserCredentialsResets>();
        services.AddTransient<IPostConfigureOptions<CookieAuthenticationOptions>, CredentialsResetCookieValidation>();

        _services = services.BuildServiceProvider();
    }

    public ValueTask DisposeAsync()
        => _services.DisposeAsync();

    [Fact]
    public async Task ACookieIssuedBeforeTheReset_IsRefused_ThoughTheStampCheckIsNotDue()
    {
        // Arrange: signed in five minutes ago, well inside the stamp check's 30 minutes; the credentials reset now.
        var now = DateTimeOffset.UtcNow;
        var cookie = Issue(now.AddMinutes(-5));
        await _services.GetRequiredService<UserCredentialsResets>().PublishAsync(UserId, now.UtcDateTime, TestContext.Current.CancellationToken);

        // Act
        var (result, response) = await AuthenticateAsync(cookie);

        // Assert: refused, and the browser told to drop the cookie.
        Assert.False(result.Succeeded);
        Assert.Contains(response.Headers.SetCookie, header => header.StartsWith(CookieName + "=;", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ACookieIssuedAtOrAfterTheReset_IsAccepted()
    {
        // Arrange: the reset, then the session it signs in again.
        var now = DateTimeOffset.UtcNow;
        await _services.GetRequiredService<UserCredentialsResets>().PublishAsync(UserId, now.UtcDateTime, TestContext.Current.CancellationToken);
        var cookie = Issue(now);

        // Act
        var (result, _) = await AuthenticateAsync(cookie);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(UserId, result.Principal.FindFirstValue(ClaimTypes.NameIdentifier));
    }

    [Fact]
    public async Task WithNoReset_AnEarlierCookieIsAccepted()
    {
        var (result, _) = await AuthenticateAsync(Issue(DateTimeOffset.UtcNow.AddMinutes(-5)));

        Assert.True(result.Succeeded);
    }

    private string CookieName => Options.Cookie.Name;

    private CookieAuthenticationOptions Options
        => _services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(IdentityConstants.ApplicationScheme);

    // A sign-in cookie, protected the way the handler protects the ones it issues.
    private string Issue(DateTimeOffset issuedUtc)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, UserId),
                new Claim(ClaimTypes.Name, "pat"),
                new Claim(new IdentityOptions().ClaimsIdentity.SecurityStampClaimType, "stamp-before-the-reset"),
            ],
            IdentityConstants.ApplicationScheme);

        var properties = new AuthenticationProperties { IssuedUtc = issuedUtc, ExpiresUtc = issuedUtc.AddHours(1) };

        return Options.TicketDataFormat.Protect(new AuthenticationTicket(new ClaimsPrincipal(identity), properties, IdentityConstants.ApplicationScheme));
    }

    private async Task<(AuthenticateResult Result, HttpResponse Response)> AuthenticateAsync(string cookie)
    {
        await using var scope = _services.CreateAsyncScope();

        var httpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        httpContext.Request.Headers.Cookie = $"{CookieName}={cookie}";

        var result = await httpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);

        return (result, httpContext.Response);
    }
}

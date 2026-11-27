using CrestApps.OrchardCore.Users;
using CrestApps.OrchardCore.Users.Controllers;
using CrestApps.OrchardCore.Users.Core.Services;
using CrestApps.OrchardCore.Users.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Users;
using OrchardCore.Users.Events;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Tests.Modules.Users;

/// <summary>
/// Review 11 (C9, C10 and the timing note): e-mail code sign-in over the real code store, attempt limiter, user manager
/// and Orchard Core's user store on SQLite. A code has eight digits; a network's wrong codes for an account stop its new
/// codes for a day, never the owner's elsewhere; an account gets at most ten codes a day from one network and fifty in
/// all, and every form of its address shares one send count; the pages do the same work for an address without an
/// account; and signing in by code to an account whose credentials nobody proved ends every other session and token at
/// once, and holds those credentials until the owner confirms.
/// </summary>
public sealed class EmailSignInGuessingTests : IAsyncLifetime
{
    private const string Email = "kate@example.test";

    private EmailSignInHarness _harness;

    // Identity's lockout only paces guessing, and a site may turn it off: these show the code's own limits.
    public async ValueTask InitializeAsync()
        => _harness = await EmailSignInHarness.CreateAsync(services => services.Configure<IdentityOptions>(options => options.Lockout.MaxFailedAccessAttempts = 1000));

    public async ValueTask DisposeAsync()
        => await _harness.DisposeAsync();

    [Fact]
    public async Task ACode_HasEightDigits()
    {
        // Arrange
        await CreateUserAsync();

        // Act
        var code = await RequestCodeAsync();

        // Assert
        Assert.Matches("^[0-9]{8}$", code);
    }

    [Fact]
    public async Task TenWrongCodesFromOneNetwork_StopNewCodesForThatNetworkForADay_NotTheOwnersElsewhere()
    {
        // Arrange: two codes, each guessed wrong as often as a code allows, from one network.
        await CreateUserAsync();
        _harness.RemoteAddress = "198.51.100.9";

        for (var round = 0; round < 2; round++)
        {
            var wrong = Wrong(await RequestCodeAsync());

            for (var i = 0; i < EmailSignInCodes.MaxAttempts; i++)
            {
                await PostCodeAsync(wrong);
            }

            _harness.Now = _harness.Now.AddMinutes(16);
        }

        var sent = _harness.Sender.Sent.Count;

        // Act: round 2, #4 - the guesser's network gets no new code, but the owner on another network signs in.
        await AskForCodeAsync(Email);
        var sentToTheGuesser = _harness.Sender.Sent.Count - sent;

        _harness.RemoteAddress = "203.0.113.7";
        var owner = await PostCodeAsync(await RequestCodeAsync());

        // Assert
        Assert.Equal(0, sentToTheGuesser);
        Assert.IsType<LocalRedirectResult>(owner);
        Assert.Single(_harness.SignIns);
    }

    [Fact]
    public async Task AnAccount_IsSentAtMostFiftyCodesADay_FromEveryNetworkTogether()
    {
        // Arrange
        await CreateUserAsync();

        // Act: ten networks, each asking within its own limits.
        for (var network = 0; network < 10; network++)
        {
            _harness.RemoteAddress = "198.51.100." + (network + 1);

            for (var i = 0; i < 6; i++)
            {
                await AskForCodeAsync(Email);
                _harness.Now = _harness.Now.AddMinutes(16);
            }
        }

        // Assert
        Assert.Equal(50, _harness.Sender.Sent.Count);
    }

    [Fact]
    public async Task AnAccount_IsSentAtMostTenCodesADay_FromOneNetwork()
    {
        // Arrange
        await CreateUserAsync();

        // Act: one ask every 16 minutes, inside the per-address limit of three each 15 minutes.
        for (var i = 0; i < 11; i++)
        {
            await AskForCodeAsync(Email);
            _harness.Now = _harness.Now.AddMinutes(16);
        }

        // Assert
        Assert.Equal(10, _harness.Sender.Sent.Count);
    }

    [Fact]
    public async Task EveryFormOfAnAddress_SharesItsSendCount()
    {
        // Arrange: three codes for the address, the most it gets in 15 minutes.
        await CreateUserAsync();

        for (var i = 0; i < 3; i++)
        {
            await AskForCodeAsync(Email);
        }

        // Act: the Kelvin sign finds the same account, as Orchard Core's lookup normalizes the address.
        await AskForCodeAsync("Kate@example.test");

        // Assert
        Assert.Equal(3, _harness.Sender.Sent.Count);
    }

    [Fact]
    public async Task TheRequestPage_DoesTheSameWork_ForAnAddressWithNoAccount()
    {
        // Arrange
        await CreateUserAsync();

        // Act
        _harness.RecordingCache.Clear();
        await AskForCodeAsync(Email);
        var forAnAccount = _harness.RecordingCache.Operations;

        _harness.RecordingCache.Clear();
        await AskForCodeAsync("nobody@example.test");
        var forNoAccount = _harness.RecordingCache.Operations;

        // Assert
        Assert.Single(_harness.Sender.Sent);
        Assert.Equal(forAnAccount, forNoAccount);
    }

    [Fact]
    public async Task TheCodePage_DoesTheSameWork_ForAnAddressWithNoAccount()
    {
        // Arrange: a code was asked for both addresses.
        await CreateUserAsync();
        var wrong = Wrong(await RequestCodeAsync());
        await AskForCodeAsync("nobody@example.test");

        // Act
        _harness.RecordingCache.Clear();
        await PostCodeAsync(wrong);
        var forAnAccount = _harness.RecordingCache.Operations;

        _harness.RecordingCache.Clear();
        await PostCodeAsync(wrong, "nobody@example.test");
        var forNoAccount = _harness.RecordingCache.Operations;

        // Assert
        Assert.Equal(forAnAccount, forNoAccount);
    }

    [Fact]
    public async Task ACodeSignIn_ToAnAccountWithCredentialsInDoubt_EndsEveryOtherSessionAndToken()
    {
        // Arrange: whoever registered with the address set a password and linked GitHub; it was never proved. Their
        // browser signed in five minutes ago.
        var stampBefore = await CreateUserAsync();
        var squatterSignedIn = _harness.Now.AddMinutes(-5);

        // Act: the owner signs in with a code.
        var result = await PostCodeAsync(await RequestCodeAsync());

        // Assert: signed in with nothing removed or proved, as before (MD1, K3)...
        Assert.IsType<LocalRedirectResult>(result);

        var account = await FindAsync();
        Assert.True(await CheckPasswordAsync(account));
        Assert.Single(account.LoginInfos);
        Assert.False(UserEmailOwnership.IsVerified(account.Properties, account.Email));

        var signIn = Assert.Single(_harness.SignIns);
        Assert.Contains(signIn.Claims, claim => claim.Type == UserCredentialsProof.AuthenticationMethodReferenceClaim && claim.Value == UserCredentialsProof.EmailedCodeMethod);
        Assert.Contains(signIn.Claims, claim => claim.Type == UserCredentialsProof.CodeSessionCredentialsClaim && claim.Value == UserCredentialsProof.CredentialsFingerprint(account));

        // ...but every other sign-in ends now: a new stamp, a cookie issued before refused at once, OpenID tokens revoked.
        Assert.NotEqual(stampBefore, account.SecurityStamp);
        Assert.True(UserCredentialsProof.AreUnprovedCredentialsHeld(account));
        Assert.True(await _harness.CredentialsResets.IsBeforeResetAsync("user-1", squatterSignedIn, stampBefore, TestContext.Current.CancellationToken));
        Assert.False(await _harness.CredentialsResets.IsBeforeResetAsync("user-1", _harness.Now, account.SecurityStamp, TestContext.Current.CancellationToken));
        Assert.Equal(["user-1"], _harness.Revoked);
    }

    [Fact]
    public async Task AfterTheOwnersCodeSignIn_TheUnprovedPassword_SignsNobodyIn_UntilTheOwnerConfirms()
    {
        // Round 2, #4: whoever registered with the owner's address signed straight back in with their password after
        // the owner's code sign-in, and could then keep the owner from ever confirming.
        await CreateUserAsync();
        await PostCodeAsync(await RequestCodeAsync());

        var account = await FindAsync();
        var loginEvent = CreateOwnershipLoginEvent(new DefaultHttpContext());

        // Act
        var passwordWhileHeld = await loginEvent.ValidatingLoginAsync(account);

        var codeContext = new DefaultHttpContext();
        EmailOwnershipLoginFormEvent.MarkEmailedCodeSignIn(codeContext);
        var codeWhileHeld = await CreateOwnershipLoginEvent(codeContext).ValidatingLoginAsync(account);

        UserCredentialsProof.ReleaseUnprovedCredentials(account);
        var passwordOnceReleased = await loginEvent.ValidatingLoginAsync(account);

        // Assert
        Assert.IsType<RedirectToRouteResult>(passwordWhileHeld);
        Assert.Null(codeWhileHeld);
        Assert.Null(passwordOnceReleased);
    }

    [Fact]
    public async Task ARefusedCodeSignIn_ToAnAccountWithCredentialsInDoubt_EndsNothing()
    {
        // Arrange
        var stampBefore = await CreateUserAsync();
        var code = await RequestCodeAsync();

        // Act
        var result = await _harness.InRequestAsync(services => _harness.CreateController(services, [new RefusingLoginFormEvent()]).Code(new EmailSignInViewModel { Email = Email, Code = code }));

        // Assert
        Assert.IsType<ForbidResult>(result);
        Assert.Equal(stampBefore, (await FindAsync()).SecurityStamp);
        Assert.Empty(_harness.Revoked);
        Assert.Empty(_harness.SignIns);
    }

    private static EmailOwnershipLoginFormEvent CreateOwnershipLoginEvent(HttpContext httpContext)
        => new(
            Mock.Of<IEmailOwnershipConfirmationPage>(),
            Mock.Of<INotifier>(),
            new HttpContextAccessor { HttpContext = httpContext },
            Mock.Of<IHtmlLocalizer<EmailOwnershipLoginFormEvent>>(localizer => localizer[It.IsAny<string>()] == new LocalizedHtmlString("x", "x")),
            NullLogger<EmailOwnershipLoginFormEvent>.Instance);

    private Task<string> CreateUserAsync()
        => _harness.CreateUserAsync(new User { UserId = "user-1", UserName = "kate", Email = Email, EmailConfirmed = true, IsEnabled = true });

    private Task<IActionResult> AskForCodeAsync(string email)
        => _harness.InRequestAsync(services => _harness.CreateController(services).Index(new EmailSignInViewModel { Email = email }));

    private async Task<string> RequestCodeAsync()
    {
        var sent = _harness.Sender.Sent.Count;

        await AskForCodeAsync(Email);

        Assert.Equal(sent + 1, _harness.Sender.Sent.Count);

        return _harness.Sender.Sent[^1].Code;
    }

    private Task<IActionResult> PostCodeAsync(string code, string email = Email)
        => _harness.InRequestAsync(services => _harness.CreateController(services).Code(new EmailSignInViewModel { Email = email, Code = code }));

    private static string Wrong(string code)
        => code == "00000000" ? "11111111" : "00000000";

    private Task<User> FindAsync()
        => _harness.InRequestAsync(async services => (User)await services.GetRequiredService<UserManager<IUser>>().FindByIdAsync("user-1"));

    private Task<bool> CheckPasswordAsync(User account)
        => _harness.InRequestAsync(services => services.GetRequiredService<UserManager<IUser>>().CheckPasswordAsync(account, EmailSignInHarness.Password));

    private sealed class RefusingLoginFormEvent : LoginFormEventBase
    {
        public override Task<IActionResult> ValidatingLoginAsync(IUser user)
            => Task.FromResult<IActionResult>(new ForbidResult());
    }
}

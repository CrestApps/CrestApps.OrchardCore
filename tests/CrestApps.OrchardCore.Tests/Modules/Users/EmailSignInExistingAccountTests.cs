using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Handlers;
using CrestApps.OrchardCore.Users;
using CrestApps.OrchardCore.Users.Controllers;
using CrestApps.OrchardCore.Users.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OrchardCore.Users;
using OrchardCore.Users.Events;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Tests.Modules.Users;

/// <summary>
/// MD1 (fix-verification review): no account that existed before the ownership marker had one, so every long-standing
/// customer lost their password and external logins the first time they signed in with a code. An account with a
/// password or an external login and no proof of its address is now signed in by a code with nothing removed, marked or
/// merged; its owner confirms it is theirs, with a second code, on the confirmation page, which removes what was set
/// before the proof and only then hands over the guest orders. These run both controllers over Orchard Core's user
/// store on SQLite, with the Customers feature's merge handler.
/// </summary>
public sealed class EmailSignInExistingAccountTests : IAsyncLifetime
{
    private const string Email = "pat@example.test";

    private readonly List<MergeSeen> _merges = [];

    private EmailSignInHarness _harness;

    public async ValueTask InitializeAsync()
        => _harness = await EmailSignInHarness.CreateAsync();

    public async ValueTask DisposeAsync()
        => await _harness.DisposeAsync();

    [Fact]
    public async Task AnExistingAccount_KeepsItsPasswordAndLogins_OnItsFirstCodeSignIn()
    {
        // Arrange: a long-standing account, confirmed before the marker existed, with a password and a GitHub login.
        var stampBefore = await _harness.CreateUserAsync(new User { UserId = "user-1", UserName = "pat", Email = Email, EmailConfirmed = true, IsEnabled = true });

        // Act
        var result = await SignInWithCodeAsync();

        // Assert: signed in, and nothing was removed, marked or merged.
        Assert.IsType<LocalRedirectResult>(result);

        var account = await FindAsync("user-1");
        Assert.True(await CheckPasswordAsync(account));
        Assert.Single(account.LoginInfos);
        Assert.Equal(stampBefore, account.SecurityStamp);
        Assert.False(UserEmailOwnership.IsVerified(account.Properties, account.Email));
        Assert.Empty(_merges);
    }

    [Fact]
    public async Task AnUnconfirmedAccountWithAPassword_IsNotConfirmedByACode()
    {
        // Arrange: whoever registered with the address set the password, and never confirmed it. Confirming it now would
        // let that password sign in on a site that requires confirmed addresses.
        await _harness.CreateUserAsync(new User { UserId = "user-1", UserName = "squatter", Email = Email, IsEnabled = true });

        // Act
        await SignInWithCodeAsync();

        // Assert
        var account = await FindAsync("user-1");
        Assert.False(account.EmailConfirmed);
        Assert.True(await CheckPasswordAsync(account));
        Assert.Empty(_merges);
    }

    [Fact]
    public async Task AnAccountWithNoPasswordOrLogin_IsProvedByItsFirstCode_AndItsGuestsMerge()
    {
        // Arrange: an account made from an order, which nobody could sign in to before.
        await _harness.CreateUserAsync(new User { UserId = "user-1", UserName = "pat", Email = Email, IsEnabled = true }, withPassword: false, withLogin: false);

        // Act
        var result = await SignInWithCodeAsync();

        // Assert
        Assert.IsType<LocalRedirectResult>(result);

        var account = await FindAsync("user-1");
        Assert.True(account.EmailConfirmed);
        Assert.True(UserEmailOwnership.IsVerified(account.Properties, account.Email));
        Assert.Equal("user-1", Assert.Single(_merges).UserId);
    }

    [Fact]
    public async Task ConfirmingItsYou_RemovesWhatWasSetBefore_ThenMergesTheGuests()
    {
        // Arrange: signed in with a code, nothing removed.
        var stampBefore = await _harness.CreateUserAsync(new User { UserId = "user-1", UserName = "pat", Email = Email, EmailConfirmed = true, IsEnabled = true });
        await SignInWithCodeAsync();

        // Act: the owner asks to confirm, and posts the second code, mailed to the account's own address.
        var asked = await _harness.InRequestAsync(services => _harness.CreateConfirmController(services, "user-1").Confirm(new EmailSignInViewModel()));
        var code = _harness.Sender.Sent[^1];
        var confirmed = await _harness.InRequestAsync(services => _harness.CreateConfirmController(services, "user-1", LoginFormEvents(services)).ConfirmCode(new EmailSignInViewModel { Code = code.Code }));

        // Assert
        Assert.Equal(nameof(EmailOwnershipController.ConfirmCode), Assert.IsType<ViewResult>(asked).ViewName);
        Assert.Equal(Email, code.Email);
        Assert.IsType<LocalRedirectResult>(confirmed);

        var account = await FindAsync("user-1");
        Assert.False(await CheckPasswordAsync(account));
        Assert.Empty(account.LoginInfos);
        Assert.NotEqual(stampBefore, account.SecurityStamp);
        Assert.True(UserEmailOwnership.IsVerified(account.Properties, account.Email));

        var merge = Assert.Single(_merges);
        Assert.Equal("user-1", merge.UserId);
        Assert.False(merge.HadPassword);
        Assert.Equal(0, merge.Logins);

        // HT2: every earlier sign-in ends now, not at the next stamp check: the reset is on the user, a cookie issued
        // before it is refused, and what an OpenID server issued is revoked.
        Assert.True(account.Properties.ContainsKey(UserCredentialsResets.PropertyName));
        Assert.True(await _harness.CredentialsResets.IsBeforeResetAsync("user-1", _harness.Now.AddMinutes(-1), securityStamp: null, TestContext.Current.CancellationToken));
        Assert.Equal(["user-1"], _harness.Revoked);
    }

    [Fact]
    public async Task AWrongCode_OnTheConfirmationPage_ChangesNothing()
    {
        // Arrange
        await _harness.CreateUserAsync(new User { UserId = "user-1", UserName = "pat", Email = Email, EmailConfirmed = true, IsEnabled = true });
        await _harness.InRequestAsync(services => _harness.CreateConfirmController(services, "user-1").Confirm(new EmailSignInViewModel()));
        var wrong = _harness.Sender.Sent[^1].Code == "000000" ? "111111" : "000000";

        // Act
        var result = await _harness.InRequestAsync(services => _harness.CreateConfirmController(services, "user-1", LoginFormEvents(services)).ConfirmCode(new EmailSignInViewModel { Code = wrong }));

        // Assert
        Assert.IsType<ViewResult>(result);

        var account = await FindAsync("user-1");
        Assert.True(await CheckPasswordAsync(account));
        Assert.False(UserEmailOwnership.IsVerified(account.Properties, account.Email));
        Assert.Empty(_merges);
        Assert.Empty(_harness.Revoked);
    }

    // F1 (third fix-verification review): the mailbox and one credential don't say whose the others are. A password can
    // be set again with the mailbox alone ("Forgot password") while somebody else's external login stays, so the owner
    // who shows the password keeps only the password: the logins go, every earlier sign-in ends, and this browser is
    // signed in again.
    [Fact]
    public async Task ConfirmingWithThePassword_KeepsOnlyThePassword_EndsEarlierSignIns_AndMergesTheGuests()
    {
        // Arrange: the GitHub login on the account may be whoever registered with the address.
        var stampBefore = await _harness.CreateUserAsync(new User { UserId = "user-1", UserName = "pat", Email = Email, EmailConfirmed = true, IsEnabled = true });
        await SignInWithCodeAsync();
        await _harness.InRequestAsync(services => _harness.CreateConfirmController(services, "user-1").Confirm(new EmailSignInViewModel()));
        var code = _harness.Sender.Sent[^1].Code;
        _harness.SignIns.Clear();

        // Act
        var confirmed = await _harness.InRequestAsync(services => _harness.CreateConfirmController(services, "user-1", LoginFormEvents(services)).ConfirmCode(new EmailSignInViewModel { Code = code, Password = EmailSignInHarness.Password }));

        // Assert
        Assert.IsType<LocalRedirectResult>(confirmed);

        var account = await FindAsync("user-1");
        Assert.True(await CheckPasswordAsync(account));
        Assert.Empty(account.LoginInfos);
        Assert.NotEqual(stampBefore, account.SecurityStamp);
        Assert.True(UserEmailOwnership.IsVerified(account.Properties, account.Email));
        Assert.True(account.Properties.ContainsKey(UserCredentialsResets.PropertyName));
        Assert.True(await _harness.CredentialsResets.IsBeforeResetAsync("user-1", _harness.Now.AddMinutes(-1), securityStamp: null, TestContext.Current.CancellationToken));
        Assert.Equal(["user-1"], _harness.Revoked);
        Assert.Equal("user-1", ((User)Assert.Single(_harness.SignIns).User).UserId);

        var merge = Assert.Single(_merges);
        Assert.True(merge.HadPassword);
        Assert.Equal(0, merge.Logins);
    }

    // F1, scenario B: the owner signed in with a code, linked their own login, and Orchard Core signed the browser in
    // again through it. Confirming keeps that login and removes the password somebody else may have set.
    [Fact]
    public async Task SignedInThroughTheAccountsLogin_ConfirmingKeepsOnlyThatLogin()
    {
        // Arrange
        var stampBefore = await _harness.CreateUserAsync(new User { UserId = "user-1", UserName = "pat", Email = Email, EmailConfirmed = true, IsEnabled = true });
        var signedInThroughGitHub = await _harness.SignInClaimsAsync("user-1", new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.AuthenticationMethod, "GitHub"));
        var asked = await _harness.InRequestAsync(services => _harness.CreateConfirmController(services, "user-1", null, signedInThroughGitHub).Confirm(new EmailSignInViewModel()));
        var code = _harness.Sender.Sent[^1].Code;

        // Act
        var confirmed = await _harness.InRequestAsync(services => _harness.CreateConfirmController(services, "user-1", LoginFormEvents(services), signedInThroughGitHub).ConfirmCode(new EmailSignInViewModel { Code = code }));

        // Assert
        Assert.True(Assert.IsType<EmailSignInViewModel>(Assert.IsType<ViewResult>(asked).Model).CredentialsShown);
        Assert.IsType<LocalRedirectResult>(confirmed);

        var account = await FindAsync("user-1");
        Assert.False(await CheckPasswordAsync(account));
        Assert.Equal("GitHub", Assert.Single(account.LoginInfos).LoginProvider);
        Assert.NotEqual(stampBefore, account.SecurityStamp);
        Assert.True(UserEmailOwnership.IsVerified(account.Properties, account.Email));
        Assert.Equal(["user-1"], _harness.Revoked);
        Assert.Single(_harness.SignIns);

        var merge = Assert.Single(_merges);
        Assert.False(merge.HadPassword);
        Assert.Equal(1, merge.Logins);
    }

    // F1: a sign-in names the provider, not which of its accounts was used, so with two logins of that provider neither
    // is known to be the owner's.
    [Fact]
    public async Task SignedInThroughAProviderTheAccountHasTwoLoginsWith_ConfirmingKeepsNeither()
    {
        // Arrange
        var user = new User { UserId = "user-1", UserName = "pat", Email = Email, EmailConfirmed = true, IsEnabled = true };
        user.LoginInfos.Add(new UserLoginInfo("GitHub", "github-someone-else", "GitHub"));
        await _harness.CreateUserAsync(user);
        var signedInThroughGitHub = await _harness.SignInClaimsAsync("user-1", new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.AuthenticationMethod, "GitHub"));
        await _harness.InRequestAsync(services => _harness.CreateConfirmController(services, "user-1", null, signedInThroughGitHub).Confirm(new EmailSignInViewModel()));
        var code = _harness.Sender.Sent[^1].Code;

        // Act
        await _harness.InRequestAsync(services => _harness.CreateConfirmController(services, "user-1", LoginFormEvents(services), signedInThroughGitHub).ConfirmCode(new EmailSignInViewModel { Code = code }));

        // Assert
        var account = await FindAsync("user-1");
        Assert.False(await CheckPasswordAsync(account));
        Assert.Empty(account.LoginInfos);
        Assert.True(UserEmailOwnership.IsVerified(account.Properties, account.Email));
    }

    // Mild (third fix-verification review): on an account with nothing to remove, the first proof still rotated the
    // stamp and recorded the reset, which ended this browser's sign-in too, and only a removal signed it in again.
    [Fact]
    public async Task ConfirmingAnAccountWithNoCredentials_SignsThisBrowserInAgain()
    {
        // Arrange: an account made from an order, signed in, whose address was never proved.
        await _harness.CreateUserAsync(new User { UserId = "user-1", UserName = "pat", Email = Email, EmailConfirmed = true, IsEnabled = true }, withPassword: false, withLogin: false);
        await _harness.InRequestAsync(services => _harness.CreateConfirmController(services, "user-1").Confirm(new EmailSignInViewModel()));
        var code = _harness.Sender.Sent[^1].Code;

        // Act
        var confirmed = await _harness.InRequestAsync(services => _harness.CreateConfirmController(services, "user-1", LoginFormEvents(services)).ConfirmCode(new EmailSignInViewModel { Code = code }));

        // Assert: the reset ended earlier sign-ins, and this browser was signed in again after it.
        Assert.IsType<LocalRedirectResult>(confirmed);
        Assert.True(await _harness.CredentialsResets.IsBeforeResetAsync("user-1", _harness.Now.AddMinutes(-1), securityStamp: null, TestContext.Current.CancellationToken));
        Assert.Equal("user-1", ((User)Assert.Single(_harness.SignIns).User).UserId);
    }

    // F2 (third fix-verification review): an account that signs in only through an external provider couldn't confirm
    // its address at all, so the guest orders placed with it never reached it. Confirming signs nobody in, so the code
    // sign-in's rule against such accounts doesn't apply to it.
    [Fact]
    public async Task AnAccountThatSignsInOnlyThroughItsLogin_CanConfirm_WhenSignedInThroughIt()
    {
        // Arrange
        await _harness.CreateUserAsync(new User { UserId = "user-1", UserName = "pat", Email = Email, EmailConfirmed = true, IsEnabled = true }, withPassword: false);
        var signedInThroughGitHub = await _harness.SignInClaimsAsync("user-1", new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.AuthenticationMethod, "GitHub"));

        // Act
        var page = await _harness.InRequestAsync(services => _harness.CreateConfirmController(services, "user-1", null, signedInThroughGitHub).Confirm(returnUrl: null));

        // Assert
        Assert.True(Assert.IsType<EmailSignInViewModel>(Assert.IsType<ViewResult>(page).Model).CredentialsShown);
    }

    // F3 (third fix-verification review): a code signs the address's owner in to an account whose password somebody else
    // may hold. The session says how it began, so every page can say so until the owner confirms.
    [Fact]
    public async Task ACodeSignIn_ToAnAccountWithCredentials_IsMarkedAsBegunWithAnEmailedCode()
    {
        // Arrange
        await _harness.CreateUserAsync(new User { UserId = "user-1", UserName = "pat", Email = Email, EmailConfirmed = true, IsEnabled = true });

        // Act
        await SignInWithCodeAsync();

        // Assert
        var signIn = Assert.Single(_harness.SignIns);
        Assert.Contains(signIn.Claims, claim => claim.Type == "amr" && claim.Value == "otp");
    }

    [Fact]
    public async Task AWrongPassword_IsRefused_BeforeTheCode_SoTheCodeStillWorks()
    {
        // Arrange
        await _harness.CreateUserAsync(new User { UserId = "user-1", UserName = "pat", Email = Email, EmailConfirmed = true, IsEnabled = true });
        await _harness.InRequestAsync(services => _harness.CreateConfirmController(services, "user-1").Confirm(new EmailSignInViewModel()));
        var code = _harness.Sender.Sent[^1].Code;

        // Act
        var refused = await _harness.InRequestAsync(services => _harness.CreateConfirmController(services, "user-1", LoginFormEvents(services)).ConfirmCode(new EmailSignInViewModel { Code = code, Password = "not-the-password" }));
        var afterRefusal = await FindAsync("user-1");
        var confirmed = await _harness.InRequestAsync(services => _harness.CreateConfirmController(services, "user-1", LoginFormEvents(services)).ConfirmCode(new EmailSignInViewModel { Code = code }));

        // Assert: nothing changed for the wrong password, and the same code then confirmed the address without it.
        Assert.IsType<ViewResult>(refused);
        Assert.False(UserEmailOwnership.IsVerified(afterRefusal.Properties, afterRefusal.Email));
        Assert.Equal(1, afterRefusal.AccessFailedCount);
        Assert.IsType<LocalRedirectResult>(confirmed);
        Assert.False(await CheckPasswordAsync(await FindAsync("user-1")));
    }

    [Fact]
    public async Task SignedInWithThePassword_ConfirmingKeepsIt()
    {
        // Arrange: a session a password began carries amr=pwd.
        await _harness.CreateUserAsync(new User { UserId = "user-1", UserName = "pat", Email = Email, EmailConfirmed = true, IsEnabled = true });
        var signedInWithPassword = await _harness.SignInClaimsAsync("user-1", new System.Security.Claims.Claim("amr", "pwd"));
        var asked = await _harness.InRequestAsync(services => _harness.CreateConfirmController(services, "user-1", null, signedInWithPassword).Confirm(new EmailSignInViewModel()));
        var code = _harness.Sender.Sent[^1].Code;

        // Act
        await _harness.InRequestAsync(services => _harness.CreateConfirmController(services, "user-1", LoginFormEvents(services), signedInWithPassword).ConfirmCode(new EmailSignInViewModel { Code = code }));

        // Assert
        var view = Assert.IsType<EmailSignInViewModel>(Assert.IsType<ViewResult>(asked).Model);
        Assert.True(view.CredentialsShown);
        Assert.False(view.AsksPassword);

        var account = await FindAsync("user-1");
        Assert.True(await CheckPasswordAsync(account));
        Assert.True(UserEmailOwnership.IsVerified(account.Properties, account.Email));
        Assert.True(Assert.Single(_merges).HadPassword);
    }

    private async Task<IActionResult> SignInWithCodeAsync()
    {
        await _harness.InRequestAsync(services => _harness.CreateController(services).Index(new EmailSignInViewModel { Email = Email }));
        var code = _harness.Sender.Sent[^1].Code;

        return await _harness.InRequestAsync(services => _harness.CreateController(services, LoginFormEvents(services)).Code(new EmailSignInViewModel { Email = Email, Code = code }));
    }

    // The Customers feature's handler, resolving the merge service when the event fires, as in production.
    private ILoginFormEvent[] LoginFormEvents(IServiceProvider requestServices)
    {
        var mergeServices = new ServiceCollection()
            .AddSingleton<ICustomerMergeService>(new RecordingMergeService(requestServices.GetRequiredService<UserManager<IUser>>(), _merges))
            .BuildServiceProvider();

        return [new CustomerMergeLoginFormEvent(mergeServices, NullLogger<CustomerMergeLoginFormEvent>.Instance)];
    }

    private Task<User> FindAsync(string userId)
        => _harness.InRequestAsync(async services => (User)await services.GetRequiredService<UserManager<IUser>>().FindByIdAsync(userId));

    private Task<bool> CheckPasswordAsync(User account)
        => _harness.InRequestAsync(services => services.GetRequiredService<UserManager<IUser>>().CheckPasswordAsync(account, EmailSignInHarness.Password));

    private sealed record MergeSeen(string UserId, bool HadPassword, int Logins);

    // Records what the account could be signed in with when its guests were merged into it, in the request that merged.
    private sealed class RecordingMergeService : ICustomerMergeService
    {
        private readonly UserManager<IUser> _manager;
        private readonly List<MergeSeen> _merges;

        public RecordingMergeService(UserManager<IUser> manager, List<MergeSeen> merges)
        {
            _manager = manager;
            _merges = merges;
        }

        public async Task<int> MergeVerifiedGuestsAsync(string userId, CancellationToken cancellationToken = default)
        {
            var account = await _manager.FindByIdAsync(userId);

            _merges.Add(new MergeSeen(userId, await _manager.HasPasswordAsync(account), (await _manager.GetLoginsAsync(account)).Count));

            return 1;
        }
    }
}

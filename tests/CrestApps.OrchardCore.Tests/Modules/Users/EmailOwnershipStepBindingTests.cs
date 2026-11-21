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
/// Review 10: what a step of the "Confirm it's you" flow established is posted back in a protected token, and the
/// account can change between the page and the post without its security stamp changing. A1: Orchard Core's profile
/// editor changes the address without rotating the stamp, so a take-back proved with the old mailbox verified the new
/// one and handed over its guest orders. B6: adding a passkey (or a login) keeps the stamp, so the old offer removed it
/// too. A4: a kept-login step that expired while the code was still valid kept nothing. A5: take-back saved the proof,
/// removed the logins and ended the sessions before a login policy could refuse the sign-in. These run both
/// controllers over Orchard Core's user store on SQLite, with the Customers feature's merge handler.
/// </summary>
public sealed class EmailOwnershipStepBindingTests : IAsyncLifetime
{
    private const string Email = "pat@example.test";

    private readonly List<string> _merges = [];

    private EmailSignInHarness _harness;

    public async ValueTask InitializeAsync()
        => _harness = await EmailSignInHarness.CreateAsync();

    public async ValueTask DisposeAsync()
        => await _harness.DisposeAsync();

    // A1: the address changed in the profile editor, with the stamp unchanged, after the code reached the old one.
    [Fact]
    public async Task ATakeBackChoice_IsRefused_OnceTheAddressChanged_ThoughTheStampDidNot()
    {
        // Arrange
        var stamp = await CreateExternalOnlyAccountAsync();
        var offer = await TakeBackOfferAsync();
        await _harness.InRequestAsync(async services =>
        {
            // What Orchard Core's user editor does: it sets the address and updates the account, keeping the stamp.
            var manager = services.GetRequiredService<UserManager<IUser>>();
            var account = (User)await manager.FindByIdAsync("user-1");
            account.Email = "someone-else@example.test";

            return await manager.UpdateAsync(account);
        });

        Assert.Equal(stamp, (await FindAsync("user-1")).SecurityStamp);

        // Act
        var result = await TakeBackAsync(offer.Token);

        // Assert
        Assert.IsType<ViewResult>(result);
        var account = await FindAsync("user-1");
        Assert.Single(account.LoginInfos);
        Assert.False(UserEmailOwnership.IsVerified(account.Properties, account.Email));
        Assert.Empty(_harness.SignIns);
        Assert.Empty(_harness.Revoked);
        Assert.Empty(_merges);
    }

    // B6: a passkey registered after the offer was shown keeps the stamp. The old offer removes nothing, and the page
    // shows the choice again with what would be removed now.
    [Fact]
    public async Task ATakeBackChoice_ShowsTheChoiceAgain_WhenAPasskeyWasAddedSince()
    {
        // Arrange
        await CreateExternalOnlyAccountAsync();
        var offer = await TakeBackOfferAsync();
        await _harness.AddPasskeyAsync("user-1");

        // Act
        var result = await TakeBackAsync(offer.Token);

        // Assert: nothing removed, and the choice now names the passkey.
        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("TakeBack", view.ViewName);
        var renewed = Assert.IsType<EmailSignInViewModel>(view.Model);
        Assert.Equal(1, renewed.Passkeys);
        Assert.NotEqual(offer.Token, renewed.Token);

        var account = await FindAsync("user-1");
        Assert.Single(account.LoginInfos);
        Assert.Single(account.Passkeys);
        Assert.False(UserEmailOwnership.IsVerified(account.Properties, account.Email));
        Assert.Empty(_harness.SignIns);
        Assert.Empty(_merges);

        // Choosing again, with what the page now shows, removes both.
        Assert.IsType<LocalRedirectResult>(await TakeBackAsync(renewed.Token));
        account = await FindAsync("user-1");
        Assert.Empty(account.LoginInfos);
        Assert.Empty(account.Passkeys);
        Assert.True(UserEmailOwnership.IsVerified(account.Properties, account.Email));
    }

    // B6: the renewed choice keeps the original step's expiry, so showing it again never extends it.
    [Fact]
    public async Task TheChoiceShownAgain_ExpiresWhenTheFirstOneDid()
    {
        // Arrange
        await CreateExternalOnlyAccountAsync();
        var offer = await TakeBackOfferAsync();
        await _harness.AddPasskeyAsync("user-1");
        var renewed = Assert.IsType<EmailSignInViewModel>(Assert.IsType<ViewResult>(await TakeBackAsync(offer.Token)).Model);

        // Act
        _harness.Now = _harness.Now.AddMinutes(16);
        var result = await TakeBackAsync(renewed.Token);

        // Assert
        Assert.Equal("Index", Assert.IsType<ViewResult>(result).ViewName);
        Assert.Single((await FindAsync("user-1")).Passkeys);
    }

    // A4: the owner signed in through GitHub again to keep it, then the step expired while the code was still valid.
    // Confirming stops before the code is spent or anything removed, and asks for the sign-in again.
    [Fact]
    public async Task AnExpiredKeptLoginStep_StopsBeforeTheCode_AndKeepsEverything()
    {
        // Arrange: the fresh sign-in, then the code asked for 14 minutes later, then posted 2 minutes after that.
        await _harness.CreateUserAsync(new User { UserId = "user-1", UserName = "pat", Email = Email, EmailConfirmed = true, IsEnabled = true }, withPassword: false);
        _harness.ExternalLogin = (new ExternalLoginInfo(new System.Security.Claims.ClaimsPrincipal(), "GitHub", "github-user-1", "GitHub"), "user-1");
        var token = Assert.IsType<EmailSignInViewModel>(Assert.IsType<ViewResult>(await _harness.InRequestAsync(services => _harness.CreateConfirmController(services, "user-1").ConfirmLoginCallback())).Model).Token;
        _harness.Now = _harness.Now.AddMinutes(14);
        await _harness.InRequestAsync(services => _harness.CreateConfirmController(services, "user-1").Confirm(new EmailSignInViewModel { Token = token }));
        var code = _harness.Sender.Sent[^1].Code;
        _harness.Now = _harness.Now.AddMinutes(2);

        // Act
        var result = await _harness.InRequestAsync(services => _harness.CreateConfirmController(services, "user-1", LoginFormEvents(services)).ConfirmCode(new EmailSignInViewModel { Code = code, Token = token }));

        // Assert: the confirmation page again, offering to keep GitHub, with nothing changed.
        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(nameof(EmailOwnershipController.Confirm), view.ViewName);
        var model = Assert.IsType<EmailSignInViewModel>(view.Model);
        Assert.Null(model.Token);
        Assert.Equal("GitHub", Assert.Single(model.KeepableLogins).Key);

        var account = await FindAsync("user-1");
        Assert.Single(account.LoginInfos);
        Assert.False(UserEmailOwnership.IsVerified(account.Properties, account.Email));
        Assert.Empty(_harness.Revoked);
        Assert.Empty(_merges);

        // The code wasn't spent: it still confirms, without the login.
        Assert.IsType<LocalRedirectResult>(await _harness.InRequestAsync(services => _harness.CreateConfirmController(services, "user-1", LoginFormEvents(services)).ConfirmCode(new EmailSignInViewModel { Code = code })));
    }

    // A5: a login policy (Tenant Hierarchy's LinkedUserLoginFormEvent, for one) refuses the sign-in. Taking the account
    // back changes nothing then: its logins and sessions stay, and its address isn't recorded as proved.
    [Fact]
    public async Task ARefusedTakeBack_KeepsTheLoginsAndTheSessions()
    {
        // Arrange
        await CreateExternalOnlyAccountAsync();
        var offer = await TakeBackOfferAsync();

        // Act
        var result = await _harness.InRequestAsync(services => _harness.CreateController(services, [new RefusingLoginFormEvent(), .. LoginFormEvents(services)]).TakeBack(new EmailSignInViewModel { Token = offer.Token }));

        // Assert
        Assert.IsType<ForbidResult>(result);
        await AssertNothingChangedAsync();
    }

    // A5: the same for an account a code proves outright (no credentials), and for the confirmation page.
    [Fact]
    public async Task ARefusedCodeSignIn_ToAnAccountACodeProves_ChangesNothing()
    {
        // Arrange
        await _harness.CreateUserAsync(new User { UserId = "user-1", UserName = "pat", Email = Email, EmailConfirmed = false, IsEnabled = true }, withPassword: false, withLogin: false);
        await _harness.InRequestAsync(services => _harness.CreateController(services).Index(new EmailSignInViewModel { Email = Email }));
        var code = _harness.Sender.Sent[^1].Code;

        // Act
        var result = await _harness.InRequestAsync(services => _harness.CreateController(services, [new RefusingLoginFormEvent(), .. LoginFormEvents(services)]).Code(new EmailSignInViewModel { Email = Email, Code = code }));

        // Assert
        Assert.IsType<ForbidResult>(result);
        var account = await FindAsync("user-1");
        Assert.False(account.EmailConfirmed);
        Assert.False(UserEmailOwnership.IsVerified(account.Properties, account.Email));
        Assert.Empty(_harness.SignIns);
        Assert.Empty(_harness.Revoked);
    }

    [Fact]
    public async Task ARefusedConfirmation_KeepsTheCredentialsAndTheSessions()
    {
        // Arrange
        await CreateExternalOnlyAccountAsync();
        await _harness.InRequestAsync(services => _harness.CreateConfirmController(services, "user-1").Confirm(new EmailSignInViewModel()));
        var code = _harness.Sender.Sent[^1].Code;

        // Act
        var result = await _harness.InRequestAsync(services => _harness.CreateConfirmController(services, "user-1", [new RefusingLoginFormEvent(), .. LoginFormEvents(services)]).ConfirmCode(new EmailSignInViewModel { Code = code }));

        // Assert
        Assert.IsType<ForbidResult>(result);
        await AssertNothingChangedAsync();
    }

    private async Task AssertNothingChangedAsync()
    {
        var account = await FindAsync("user-1");
        Assert.Single(account.LoginInfos);
        Assert.False(UserEmailOwnership.IsVerified(account.Properties, account.Email));
        Assert.False(account.Properties.ContainsKey(UserCredentialsResets.PropertyName));
        Assert.False(await _harness.CredentialsResets.IsBeforeResetAsync("user-1", _harness.Now.AddMinutes(-1), securityStamp: null, TestContext.Current.CancellationToken));
        Assert.Empty(_harness.SignIns);
        Assert.Empty(_harness.Revoked);
        Assert.Empty(_merges);
    }

    private Task<string> CreateExternalOnlyAccountAsync()
        => _harness.CreateUserAsync(new User { UserId = "user-1", UserName = "pat", Email = Email, EmailConfirmed = true, IsEnabled = true }, withPassword: false);

    private async Task<EmailSignInViewModel> TakeBackOfferAsync()
    {
        await _harness.InRequestAsync(services => _harness.CreateController(services).Index(new EmailSignInViewModel { Email = Email }));
        var code = _harness.Sender.Sent[^1].Code;
        var offer = await _harness.InRequestAsync(services => _harness.CreateController(services, LoginFormEvents(services)).Code(new EmailSignInViewModel { Email = Email, Code = code }));

        return Assert.IsType<EmailSignInViewModel>(Assert.IsType<ViewResult>(offer).Model);
    }

    private Task<IActionResult> TakeBackAsync(string token)
        => _harness.InRequestAsync(services => _harness.CreateController(services, LoginFormEvents(services)).TakeBack(new EmailSignInViewModel { Token = token }));

    // The Customers feature's handler, resolving the merge service when the event fires, as in production.
    private ILoginFormEvent[] LoginFormEvents(IServiceProvider requestServices)
    {
        var mergeServices = new ServiceCollection()
            .AddSingleton<ICustomerMergeService>(new RecordingMergeService(_merges))
            .BuildServiceProvider();

        return [new CustomerMergeLoginFormEvent(mergeServices, NullLogger<CustomerMergeLoginFormEvent>.Instance)];
    }

    private Task<User> FindAsync(string userId)
        => _harness.InRequestAsync(async services => (User)await services.GetRequiredService<UserManager<IUser>>().FindByIdAsync(userId));

    // A login policy that refuses every sign-in, as Tenant Hierarchy's does for a linked user.
    private sealed class RefusingLoginFormEvent : LoginFormEventBase
    {
        public override Task<IActionResult> ValidatingLoginAsync(IUser user)
            => Task.FromResult<IActionResult>(new ForbidResult());
    }

    private sealed class RecordingMergeService : ICustomerMergeService
    {
        private readonly List<string> _merges;

        public RecordingMergeService(List<string> merges)
            => _merges = merges;

        public Task<int> MergeVerifiedGuestsAsync(string userId, CancellationToken cancellationToken = default)
        {
            _merges.Add(userId);

            return Task.FromResult(1);
        }
    }
}

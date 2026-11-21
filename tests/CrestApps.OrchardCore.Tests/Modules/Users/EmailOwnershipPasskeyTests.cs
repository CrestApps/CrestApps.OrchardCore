using System.Security.Claims;
using CrestApps.OrchardCore.Users;
using CrestApps.OrchardCore.Users.Controllers;
using CrestApps.OrchardCore.Users.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using OrchardCore.Users.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Users;

/// <summary>
/// Orchard Core 4.0.0-preview-19231's user store keeps passkeys (<see cref="IUserPasskeyStore{TUser}"/>), and a passkey
/// signs in like a password or an external login. Whoever registered with somebody else's address can add one before the
/// owner proves it, so the first proof of the address removes the account's passkeys with its other credentials, an
/// account that signs in only with passkeys gets the same choice as one that signs in only through external logins (K4),
/// and a passkey session (<c>amr=pop</c>, or <c>amr=mfa</c> when the site requires user verification) is never taken for
/// a password session. These run both controllers over ASP.NET Core Identity's user manager and Orchard Core's user
/// store on SQLite; the passkeys are registered through <see cref="UserManager{TUser}.AddOrUpdatePasskeyAsync"/>.
/// </summary>
public sealed class EmailOwnershipPasskeyTests : IAsyncLifetime
{
    private const string Email = "pat@example.test";

    private EmailSignInHarness _harness;

    public async ValueTask InitializeAsync()
        => _harness = await EmailSignInHarness.CreateAsync();

    public async ValueTask DisposeAsync()
        => await _harness.DisposeAsync();

    // What the earlier tripwire waited for: the pinned Orchard Core store keeps passkeys, so the proof has some to remove.
    [Fact]
    public void OrchardCoresUserStore_KeepsPasskeys()
        => Assert.True(typeof(IUserPasskeyStore<IUser>).IsAssignableFrom(typeof(UserStore)));

    // A squatter registered with the owner's address, set a password and added a passkey. The owner signs in with a code
    // and confirms, typing the password they reset: the password stays, the squatter's passkey does not.
    [Fact]
    public async Task TheFirstProof_RemovesThePasskeys_AndKeepsOnlyThePasswordTyped()
    {
        // Arrange
        await CreateAccountAsync(withPassword: true);
        await _harness.AddPasskeyAsync("user-1");
        await SignInWithCodeAsync();
        await _harness.InRequestAsync(services => _harness.CreateConfirmController(services, "user-1").Confirm(new EmailSignInViewModel()));
        var code = _harness.Sender.Sent[^1].Code;

        // Act
        var confirmed = await _harness.InRequestAsync(services => _harness.CreateConfirmController(services, "user-1").ConfirmCode(new EmailSignInViewModel { Code = code, Password = EmailSignInHarness.Password }));

        // Assert
        Assert.IsType<LocalRedirectResult>(confirmed);

        var account = await FindAsync();
        Assert.Empty(account.Passkeys);
        Assert.Empty(await PasskeysAsync());
        Assert.True(await CheckPasswordAsync(account));
        Assert.True(UserEmailOwnership.IsVerified(account.Properties, account.Email));
    }

    // The confirmation page says the passkeys go too, so the owner knows to add theirs again.
    [Fact]
    public async Task TheConfirmationPage_SaysThePasskeysAreRemoved()
    {
        await CreateAccountAsync(withPassword: true);
        await _harness.AddPasskeyAsync("user-1");

        var page = await _harness.InRequestAsync(services => _harness.CreateConfirmController(services, "user-1").Confirm());

        var view = Assert.IsType<EmailSignInViewModel>(Assert.IsType<ViewResult>(page).Model);
        Assert.True(view.HasCredentials);
        Assert.Equal(1, view.Passkeys);
    }

    // An account that signs in only with a passkey, whose address nobody proved, may be somebody else's registration or
    // the owner's own passwordless account. A right code removes nothing by itself: it asks, as for external logins.
    [Fact]
    public async Task ACode_OffersToTakeBackAnAccountWithOnlyAPasskey_AndRemovesNothingUntilChosen()
    {
        // Arrange
        await CreateAccountAsync(withPassword: false);
        await _harness.AddPasskeyAsync("user-1");

        // Act
        var result = await SignInWithCodeAsync();

        // Assert
        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("TakeBack", view.ViewName);
        Assert.Equal(1, Assert.IsType<EmailSignInViewModel>(view.Model).Passkeys);

        var account = await FindAsync();
        Assert.Single(account.Passkeys);
        Assert.False(UserEmailOwnership.IsVerified(account.Properties, account.Email));
        Assert.Empty(_harness.SignIns);
        Assert.Empty(_harness.Revoked);
    }

    [Fact]
    public async Task ChoosingToTakeBackAPasskeyOnlyAccount_RemovesItsPasskeys_ProvesTheAddress_AndSignsIn()
    {
        // Arrange
        await CreateAccountAsync(withPassword: false);
        await _harness.AddPasskeyAsync("user-1");
        var offer = Assert.IsType<EmailSignInViewModel>(Assert.IsType<ViewResult>(await SignInWithCodeAsync()).Model);

        // Act
        var result = await _harness.InRequestAsync(services => _harness.CreateController(services).TakeBack(new EmailSignInViewModel { Token = offer.Token }));

        // Assert
        Assert.IsType<LocalRedirectResult>(result);
        var account = await FindAsync();
        Assert.Empty(account.Passkeys);
        Assert.True(UserEmailOwnership.IsVerified(account.Properties, account.Email));
        Assert.Equal(["user-1"], _harness.Revoked);
        Assert.DoesNotContain(Assert.Single(_harness.SignIns).Claims, claim => claim.Type == "amr");
    }

    // An external-only account with a passkey too: taking it back removes both, or the squatter's passkey would survive.
    [Fact]
    public async Task TakingBackAnAccountWithALoginAndAPasskey_RemovesBoth()
    {
        // Arrange
        await CreateAccountAsync(withPassword: false, withLogin: true);
        await _harness.AddPasskeyAsync("user-1");
        var offer = Assert.IsType<EmailSignInViewModel>(Assert.IsType<ViewResult>(await SignInWithCodeAsync()).Model);

        // Act
        await _harness.InRequestAsync(services => _harness.CreateController(services).TakeBack(new EmailSignInViewModel { Token = offer.Token }));

        // Assert
        var account = await FindAsync();
        Assert.Empty(account.LoginInfos);
        Assert.Empty(account.Passkeys);
        Assert.True(UserEmailOwnership.IsVerified(account.Properties, account.Email));
    }

    // On a site that requires confirmed addresses, an unconfirmed account's passkey never signed anyone in (Orchard Core's
    // passkey sign-in raises the same email confirmation check), so a code proves it and removes the passkey.
    [Fact]
    public async Task AnUnconfirmedPasskeyOnlyAccount_WhereConfirmationIsRequired_IsProvedAndLosesThePasskey()
    {
        // Arrange
        _harness.Registration.UsersMustValidateEmail = true;
        await _harness.CreateUserAsync(new User { UserId = "user-1", UserName = "pat", Email = Email, IsEnabled = true }, withPassword: false, withLogin: false);
        await _harness.AddPasskeyAsync("user-1");

        // Act
        var result = await SignInWithCodeAsync();

        // Assert
        Assert.IsType<LocalRedirectResult>(result);
        var account = await FindAsync();
        Assert.Empty(account.Passkeys);
        Assert.True(account.EmailConfirmed);
        Assert.True(UserEmailOwnership.IsVerified(account.Properties, account.Email));
    }

    // Once the address is proved, the passkey is the owner's: a code signs in to the account like one with a password,
    // and removes nothing.
    [Fact]
    public async Task APasskeyOnlyAccountWhoseAddressIsProved_IsSignedInByACode_AndKeepsItsPasskey()
    {
        // Arrange
        var user = new User { UserId = "user-1", UserName = "pat", Email = Email, EmailConfirmed = true, IsEnabled = true };
        UserEmailOwnership.MarkVerified(user.Properties, Email, _harness.Now);
        await _harness.CreateUserAsync(user, withPassword: false, withLogin: false);
        await _harness.AddPasskeyAsync("user-1");

        // Act
        var result = await SignInWithCodeAsync();

        // Assert
        Assert.IsType<LocalRedirectResult>(result);
        Assert.Single((await FindAsync()).Passkeys);
    }

    [Fact]
    public async Task APasskey_CountsAsACredential()
    {
        // Arrange
        await CreateAccountAsync(withPassword: false);
        await _harness.AddPasskeyAsync("user-1", id: 1);
        await _harness.AddPasskeyAsync("user-1", id: 2);

        // Act
        var account = await FindAsync();

        // Assert
        Assert.True(UserCredentialsProof.HasCredentials(account));
        Assert.True(UserCredentialsProof.HasCredentialsInDoubt(account));
        Assert.Equal(2, UserCredentialsProof.CountCredentials(account));
    }

    // Orchard Core signs a passkey in with amr=pop, or amr=mfa when the site requires user verification, and no provider
    // name: neither is the account's password, even on an account that has one and with the current security stamp.
    [Theory]
    [InlineData("pop")]
    [InlineData("mfa")]
    public async Task APasskeySession_DoesNotShowThePassword(string method)
    {
        // Arrange
        await CreateAccountAsync(withPassword: true);
        await _harness.AddPasskeyAsync("user-1");
        var session = await SessionAsync(new Claim("amr", method));

        // Act
        var account = await FindAsync();

        // Assert
        Assert.False(UserCredentialsProof.ShowsPassword(session, account));
        Assert.False(UserCredentialsProof.ShowsCredentials(session, account));
        Assert.False(UserCredentialsProof.SignedInWithEmailedCode(session));
        Assert.True(UserCredentialsProof.HasCredentialsNotShown(session, account));
    }

    // A passkey is never shown by a session: the session doesn't say which passkey signed it in, and the proof removes
    // them all, so an account with one has a credential the session doesn't show.
    [Fact]
    public async Task APasswordSession_OnAnAccountWithAPasskey_HasACredentialItDoesntShow()
    {
        await CreateAccountAsync(withPassword: true);
        await _harness.AddPasskeyAsync("user-1");

        Assert.True(UserCredentialsProof.HasCredentialsNotShown(await SessionAsync(new Claim("amr", "pwd")), await FindAsync()));
    }

    // K3: adding or removing a passkey keeps the security stamp (ASP.NET Core Identity's AddOrUpdatePasskeyAsync and
    // RemovePasskeyAsync only update the user), so the code session's digest counts the passkeys themselves.
    [Fact]
    public async Task TheCredentialsDigest_ChangesWhenAPasskeyIsAddedOrRemoved_ButNotWhenOneIsUsed()
    {
        // Arrange
        await CreateAccountAsync(withPassword: true);
        var before = UserCredentialsProof.CredentialsFingerprint(await FindAsync());

        // Act
        var credentialId = await _harness.AddPasskeyAsync("user-1");
        var added = await FindAsync();
        var withPasskey = UserCredentialsProof.CredentialsFingerprint(added);
        await _harness.InRequestAsync(async services =>
        {
            var manager = services.GetRequiredService<UserManager<IUser>>();
            var account = await manager.FindByIdAsync("user-1");
            var passkey = await manager.GetPasskeyAsync(account, credentialId);
            passkey.SignCount = 7;

            return await manager.AddOrUpdatePasskeyAsync(account, passkey);
        });
        var afterUse = UserCredentialsProof.CredentialsFingerprint(await FindAsync());
        await _harness.RemovePasskeyAsync("user-1", credentialId);
        var removed = await FindAsync();

        // Assert
        Assert.NotEqual(before, withPasskey);
        Assert.Equal(withPasskey, afterUse);
        Assert.Equal(before, UserCredentialsProof.CredentialsFingerprint(removed));
        Assert.Equal(added.SecurityStamp, removed.SecurityStamp);
    }

    private Task<string> CreateAccountAsync(bool withPassword, bool withLogin = false)
        => _harness.CreateUserAsync(new User { UserId = "user-1", UserName = "pat", Email = Email, EmailConfirmed = true, IsEnabled = true }, withPassword, withLogin);

    private async Task<ClaimsPrincipal> SessionAsync(params Claim[] signInClaims)
        => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1"), .. await _harness.SignInClaimsAsync("user-1", signInClaims)], IdentityConstants.ApplicationScheme));

    private async Task<IActionResult> SignInWithCodeAsync()
    {
        await _harness.InRequestAsync(services => _harness.CreateController(services).Index(new EmailSignInViewModel { Email = Email }));
        var code = _harness.Sender.Sent[^1].Code;

        return await _harness.InRequestAsync(services => _harness.CreateController(services).Code(new EmailSignInViewModel { Email = Email, Code = code }));
    }

    private Task<User> FindAsync()
        => _harness.InRequestAsync(async services => (User)await services.GetRequiredService<UserManager<IUser>>().FindByIdAsync("user-1"));

    private Task<IList<UserPasskeyInfo>> PasskeysAsync()
        => _harness.InRequestAsync(async services =>
        {
            var manager = services.GetRequiredService<UserManager<IUser>>();

            return await manager.GetPasskeysAsync(await manager.FindByIdAsync("user-1"));
        });

    private Task<bool> CheckPasswordAsync(User account)
        => _harness.InRequestAsync(services => services.GetRequiredService<UserManager<IUser>>().CheckPasswordAsync(account, EmailSignInHarness.Password));
}

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
/// G1 (fourth fix-verification review): "Confirm it's you" kept the login of the provider the session's cookie named,
/// but a cookie can be older than the account's logins, so the login it kept could be one that replaced the owner's.
/// A session now shows a credential only while its cookie carries the account's current security stamp. These run the
/// confirmation pages over ASP.NET Core Identity's user manager and Orchard Core's user store and claims principal
/// factory on SQLite.
/// </summary>
public sealed class EmailOwnershipStaleSessionTests : IAsyncLifetime
{
    private const string Email = "pat@example.test";
    private const string OwnersLogin = "github-user-1";

    private EmailSignInHarness _harness;

    public async ValueTask InitializeAsync()
        => _harness = await EmailSignInHarness.CreateAsync();

    public async ValueTask DisposeAsync()
        => await _harness.DisposeAsync();

    [Fact]
    public async Task ASessionOlderThanTheAccountsLogin_ShowsNothing_SoConfirmingKeepsNoLogin()
    {
        // Arrange: the owner signed in with a code, linked their own GitHub login, and Orchard Core signed the browser
        // in again through it.
        await CreateAccountAsync();
        var ownersSession = await _harness.SignInClaimsAsync("user-1", new Claim(ClaimTypes.AuthenticationMethod, "GitHub"));

        // The password holder removes that login on the External logins page and links their own GitHub account, as
        // Orchard Core does, with Identity's user manager.
        var stamps = await _harness.InRequestAsync(async services =>
        {
            var manager = services.GetRequiredService<UserManager<IUser>>();
            var account = await manager.FindByIdAsync("user-1");
            var before = await manager.GetSecurityStampAsync(account);

            Assert.True((await manager.RemoveLoginAsync(account, "GitHub", OwnersLogin)).Succeeded);
            var afterRemove = await manager.GetSecurityStampAsync(account);

            Assert.True((await manager.AddLoginAsync(account, new UserLoginInfo("GitHub", "github-squatter", "GitHub"))).Succeeded);

            return (Before: before, AfterRemove: afterRemove, AfterAdd: await manager.GetSecurityStampAsync(account));
        });

        var asked = await _harness.InRequestAsync(services => _harness.CreateConfirmController(services, "user-1", null, ownersSession).Confirm(new EmailSignInViewModel()));
        var code = _harness.Sender.Sent[^1].Code;

        // Act
        var confirmed = await _harness.InRequestAsync(services => _harness.CreateConfirmController(services, "user-1", null, ownersSession).ConfirmCode(new EmailSignInViewModel { Code = code }));

        // Assert: removing a login rotates the stamp and adding one does not, so the owner's cookie is stale.
        Assert.NotEqual(stamps.Before, stamps.AfterRemove);
        Assert.Equal(stamps.AfterRemove, stamps.AfterAdd);

        var view = Assert.IsType<EmailSignInViewModel>(Assert.IsType<ViewResult>(asked).Model);
        Assert.Null(view.KeptLoginProvider);

        Assert.IsType<LocalRedirectResult>(confirmed);
        var account = await FindAsync();
        Assert.Empty(account.LoginInfos);
        Assert.Null(account.PasswordHash);
        Assert.True(UserEmailOwnership.IsVerified(account.Properties, account.Email));
    }

    // K5 (fifth fix-verification review): Orchard Core's "Reset authenticator app" calls RefreshSignInAsync, which
    // re-issues the owner's stale cookie with the account's current security stamp and its old provider claim. The
    // confirmation page no longer reads the cookie to decide what to keep, so the squatter's login still goes.
    [Fact]
    public async Task ASessionRefreshedWithTheCurrentStampAfterTheSwap_StillKeepsNoLogin()
    {
        // Arrange: the swap, then the owner's browser refreshed: the current stamp, and "GitHub" still in the cookie.
        await CreateAccountAsync();
        await _harness.InRequestAsync(async services =>
        {
            var manager = services.GetRequiredService<UserManager<IUser>>();
            var account = await manager.FindByIdAsync("user-1");

            Assert.True((await manager.RemoveLoginAsync(account, "GitHub", OwnersLogin)).Succeeded);
            Assert.True((await manager.AddLoginAsync(account, new UserLoginInfo("GitHub", "github-squatter", "GitHub"))).Succeeded);

            return account;
        });

        var refreshed = await _harness.SignInClaimsAsync("user-1", new Claim(ClaimTypes.AuthenticationMethod, "GitHub"));
        await _harness.InRequestAsync(services => _harness.CreateConfirmController(services, "user-1", null, refreshed).Confirm(new EmailSignInViewModel()));
        var code = _harness.Sender.Sent[^1].Code;

        // Act
        await _harness.InRequestAsync(services => _harness.CreateConfirmController(services, "user-1", null, refreshed).ConfirmCode(new EmailSignInViewModel { Code = code }));

        // Assert
        var confirmedAccount = await FindAsync();
        Assert.Empty(confirmedAccount.LoginInfos);
        Assert.True(UserEmailOwnership.IsVerified(confirmedAccount.Properties, confirmedAccount.Email));
    }

    [Fact]
    public async Task AFreshSessionThroughTheAccountsLogin_StillShowsIt()
    {
        await CreateAccountAsync();
        var session = await _harness.SignInClaimsAsync("user-1", new Claim(ClaimTypes.AuthenticationMethod, "GitHub"));

        Assert.Equal("GitHub", await ShownLoginProviderAsync(session));
    }

    [Fact]
    public async Task ASessionOrchardCoreRefreshedAtItsStampCheck_StillShowsTheLogin()
    {
        // Arrange: Identity's stamp check found the stamp unchanged, built a new principal, and Orchard Core's
        // OnRefreshingPrincipal carried the earlier claims over.
        await CreateAccountAsync();
        var current = Principal(await _harness.SignInClaimsAsync("user-1", new Claim(ClaimTypes.AuthenticationMethod, "GitHub")));
        var renewed = Principal(await _harness.SignInClaimsAsync("user-1"));

        var options = new SecurityStampValidatorOptions();
        new ConfigureSecurityStampOptions().PostConfigure(null, options);
        var context = new SecurityStampRefreshingPrincipalContext { CurrentPrincipal = current, NewPrincipal = renewed };

        // Act
        await options.OnRefreshingPrincipal(context);

        // Assert
        Assert.Equal("GitHub", await ShownLoginProviderAsync([.. context.NewPrincipal.Claims.Where(claim => claim.Type != ClaimTypes.NameIdentifier)]));
    }

    [Fact]
    public async Task APasswordSessionOlderThanAPasswordChange_DoesNotShowThePassword()
    {
        await CreateAccountAsync();
        var session = await _harness.SignInClaimsAsync("user-1", new Claim("amr", "pwd"));

        await _harness.InRequestAsync(async services =>
        {
            var manager = services.GetRequiredService<UserManager<IUser>>();
            var account = await manager.FindByIdAsync("user-1");

            Assert.True((await manager.ChangePasswordAsync(account, EmailSignInHarness.Password, "Somebody-Elses-1!")).Succeeded);

            return account;
        });

        var account = await FindAsync();
        Assert.True(UserCredentialsProof.ShowsPassword(Principal(await _harness.SignInClaimsAsync("user-1", new Claim("amr", "pwd"))), account));
        Assert.False(UserCredentialsProof.ShowsPassword(Principal(session), account));
    }

    [Fact]
    public void TheStampClaim_IsIdentitysDefault()
        => Assert.Equal(UserCredentialsProof.SecurityStampClaimType, new ClaimsIdentityOptions().SecurityStampClaimType);

    private Task<string> CreateAccountAsync()
    {
        var user = new User { UserId = "user-1", UserName = "pat", Email = Email, EmailConfirmed = true, IsEnabled = true };

        return _harness.CreateUserAsync(user);
    }

    private async Task<string> ShownLoginProviderAsync(Claim[] session)
        => UserCredentialsProof.ShownLoginProvider(Principal(session), await FindAsync());

    private static ClaimsPrincipal Principal(IEnumerable<Claim> session)
        => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1"), .. session], IdentityConstants.ApplicationScheme));

    private Task<User> FindAsync()
        => _harness.InRequestAsync(async services => (User)await services.GetRequiredService<UserManager<IUser>>().FindByIdAsync("user-1"));
}

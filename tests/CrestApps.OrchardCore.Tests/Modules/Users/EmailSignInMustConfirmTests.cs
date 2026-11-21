using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Users;
using CrestApps.OrchardCore.Users.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Users;
using OrchardCore.Users.Controllers;
using OrchardCore.Users.Events;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Tests.Modules.Users;

/// <summary>
/// Mild (third fix-verification review): on a site that requires confirmed addresses, the right code for an unconfirmed
/// account with a password ended on Orchard Core's "An email has been sent to you", with no mail sent and no way to ask
/// for one. The code was spent and nothing was confirmed, and the owner of a squatted address could not recover,
/// because password reset is refused for an unconfirmed account. Orchard Core refuses every sign-in to such an account,
/// so its password and logins never signed anyone in: the code takes the account back. These run the code page over
/// Orchard Core's user store on SQLite, with Orchard Core's own e-mail confirmation check.
/// </summary>
public sealed class EmailSignInMustConfirmTests : IAsyncLifetime
{
    private const string Email = "pat@example.test";

    private EmailSignInHarness _harness;

    public async ValueTask InitializeAsync()
    {
        _harness = await EmailSignInHarness.CreateAsync();
        _harness.Registration.UsersMustValidateEmail = true;
    }

    public async ValueTask DisposeAsync()
        => await _harness.DisposeAsync();

    [Fact]
    public async Task TheRightCode_TakesBackAnUnconfirmedAccount_AndSignsIn()
    {
        // Arrange: somebody registered with the address, a password and an external login, and never confirmed it.
        var stampBefore = await _harness.CreateUserAsync(new User { UserId = "user-1", UserName = "squatter", Email = Email, IsEnabled = true });

        // Act
        var result = await SignInWithCodeAsync();

        // Assert: signed in, with the address confirmed and proved, and nothing left that was set before.
        Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("user-1", ((User)Assert.Single(_harness.SignIns).User).UserId);

        var account = await _harness.InRequestAsync(async services => (User)await services.GetRequiredService<UserManager<IUser>>().FindByIdAsync("user-1"));
        Assert.True(account.EmailConfirmed);
        Assert.True(UserEmailOwnership.IsVerified(account.Properties, account.Email));
        Assert.True(string.IsNullOrEmpty(account.PasswordHash));
        Assert.Empty(account.LoginInfos);
        Assert.NotEqual(stampBefore, account.SecurityStamp);
        Assert.Equal(["user-1"], _harness.Revoked);
    }

    [Fact]
    public async Task AConfirmedAccount_KeepsItsPassword_AndIsOnlySignedIn()
    {
        // Arrange: the address is confirmed, so the password may have signed in; a code takes nothing.
        await _harness.CreateUserAsync(new User { UserId = "user-1", UserName = "pat", Email = Email, EmailConfirmed = true, IsEnabled = true });

        // Act
        var result = await SignInWithCodeAsync();

        // Assert
        Assert.IsType<LocalRedirectResult>(result);

        var account = await _harness.InRequestAsync(async services => (User)await services.GetRequiredService<UserManager<IUser>>().FindByIdAsync("user-1"));
        Assert.False(string.IsNullOrEmpty(account.PasswordHash));
        Assert.False(UserEmailOwnership.IsVerified(account.Properties, account.Email));
    }

    private async Task<IActionResult> SignInWithCodeAsync()
    {
        await _harness.InRequestAsync(services => _harness.CreateController(services).Index(new EmailSignInViewModel { Email = Email }));
        var code = _harness.Sender.Sent[^1].Code;

        return await _harness.InRequestAsync(services => _harness.CreateController(services, [OrchardsEmailConfirmationCheck(services)]).Code(new EmailSignInViewModel { Email = Email, Code = code }));
    }

    // Orchard Core's own login check that refuses an unconfirmed address when the site requires confirmed ones.
    private ILoginFormEvent OrchardsEmailConfirmationCheck(IServiceProvider services)
    {
        var type = typeof(EmailConfirmationController).Assembly.GetType("OrchardCore.Users.Handlers.EmailConfirmationLoginFormEvent", throwOnError: true);

        return (ILoginFormEvent)Activator.CreateInstance(
            type,
            new TestOptionsMonitor<RegistrationOptions>(_harness.Registration),
            services.GetRequiredService<UserManager<IUser>>(),
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() });
    }
}

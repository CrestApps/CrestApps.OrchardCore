using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Users;
using CrestApps.OrchardCore.Users.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Users;
using OrchardCore.Users.Handlers;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Tests.Modules.Users;

/// <summary>
/// K4 (fifth fix-verification review): no external registration recorded the address as proved, so every passwordless
/// external account was one a code could take over. An external sign-in whose provider asserts the account's own address
/// as verified now proves it, when nothing else signs in to the account. These run the handler as Orchard Core's
/// <c>ExternalSignInAsync</c> does (its <see cref="UpdateUserContext"/>, then <c>UpdateUserPropertiesAsync</c> and an
/// update), over Orchard Core's user store on SQLite.
/// </summary>
public sealed class EmailOwnershipExternalLoginHandlerTests : IAsyncLifetime
{
    private const string Email = "pat@example.test";

    private EmailSignInHarness _harness;

    public async ValueTask InitializeAsync()
        => _harness = await EmailSignInHarness.CreateAsync(services => services.AddScoped<IUserEventHandler, EmailOwnershipUserEventHandler>());

    public async ValueTask DisposeAsync()
        => await _harness.DisposeAsync();

    [Fact]
    public async Task APasswordlessAccount_WhoseOnlyLoginsProviderVerifiedItsAddress_IsProved()
    {
        await CreateAccountAsync(withPassword: false);

        Assert.True(await SignInThroughGitHubAsync(Claims(Email, "true")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("false")]
    public async Task AProviderThatDoesntSayItVerifiedTheAddress_ProvesNothing(string verified)
    {
        await CreateAccountAsync(withPassword: false);

        Assert.False(await SignInThroughGitHubAsync(Claims(Email, verified)));
    }

    // A squatter registered with the owner's address through their own provider account, which asserts its own address.
    [Fact]
    public async Task AProviderThatVerifiedAnotherAddress_ProvesNothing()
    {
        await CreateAccountAsync(withPassword: false);

        Assert.False(await SignInThroughGitHubAsync(Claims("someone@example.test", "true")));
    }

    // A password set before, by whoever it was, keeps the account waiting for "Confirm it's you".
    [Fact]
    public async Task AnAccountWithAPasswordSetEarlier_IsNotProved()
    {
        await CreateAccountAsync(withPassword: true);

        Assert.False(await SignInThroughGitHubAsync(Claims(Email, "true")));
    }

    [Fact]
    public async Task AnAccountWithAnotherLogin_IsNotProved()
    {
        var user = new User { UserId = "user-1", UserName = "pat", Email = Email, EmailConfirmed = true, IsEnabled = true };
        user.LoginInfos.Add(new UserLoginInfo("Google", "google-1", "Google"));
        await _harness.CreateUserAsync(user, withPassword: false);

        Assert.False(await SignInThroughGitHubAsync(Claims(Email, "true")));
    }

    // K3 too: Orchard Core's default external registration asks for a password. Registered in one request, the password
    // and the login are the same person's, so the provider's assertion proves the account and nobody is told anything.
    [Fact]
    public async Task ARegistrationWithAPassword_InTheSameRequest_IsProved()
    {
        var proved = await _harness.InRequestAsync(async services =>
        {
            services.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext();
            var manager = services.GetRequiredService<UserManager<IUser>>();
            var user = new User { UserId = "user-1", UserName = "pat", Email = Email, EmailConfirmed = true, IsEnabled = true };

            Assert.True((await manager.CreateAsync(user, EmailSignInHarness.Password)).Succeeded);
            Assert.True((await manager.AddLoginAsync(user, new UserLoginInfo("GitHub", "github-user-1", "GitHub"))).Succeeded);

            return await RunHandlerAsync(services, user, Claims(Email, "true"));
        });

        Assert.True(proved);
    }

    private Task<string> CreateAccountAsync(bool withPassword)
        => _harness.CreateUserAsync(new User { UserId = "user-1", UserName = "pat", Email = Email, EmailConfirmed = true, IsEnabled = true }, withPassword, withLogin: true);

    private static SerializableClaim[] Claims(string email, string verified)
    {
        List<SerializableClaim> claims =
        [
            new() { Type = ClaimTypes.NameIdentifier, Value = "github-user-1" },
            new() { Type = ClaimTypes.Email, Value = email },
        ];

        if (verified is not null)
        {
            claims.Add(new SerializableClaim { Type = "email_verified", Value = verified });
        }

        return [.. claims];
    }

    // An external sign-in through the account's GitHub login, in its own request; whether the address is proved after.
    private Task<bool> SignInThroughGitHubAsync(SerializableClaim[] claims)
        => _harness.InRequestAsync(async services =>
        {
            services.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext();
            var account = (User)await services.GetRequiredService<UserManager<IUser>>().FindByIdAsync("user-1");

            return await RunHandlerAsync(services, account, claims);
        });

    // What Orchard Core's ExternalAuthenticationsController.ExternalSignInAsync does with the handlers, then a read back.
    private static async Task<bool> RunHandlerAsync(IServiceProvider services, User account, SerializableClaim[] claims)
    {
        var manager = services.GetRequiredService<UserManager<IUser>>();
        var context = new UpdateUserContext(account, "GitHub", claims, account.Properties.DeepClone() as JsonObject);
        var handler = new EmailOwnershipExternalLoginHandler(services.GetRequiredService<IHttpContextAccessor>(), services.GetRequiredService<IClock>());

        await handler.UpdateUserAsync(context);

        if (await manager.UpdateUserPropertiesAsync(account, context))
        {
            await manager.UpdateAsync(account);
        }

        var saved = (User)await manager.FindByIdAsync(account.UserId);

        return UserEmailOwnership.IsVerified(saved.Properties, saved.Email);
    }
}

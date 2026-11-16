using System.Security.Claims;
using CrestApps.OrchardCore.Users;
using CrestApps.OrchardCore.Users.Core.Handlers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Modules;
using OrchardCore.Users;
using OrchardCore.Users.Handlers;
using OrchardCore.Users.Models;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Modules.Users;

/// <summary>
/// Owner decision 17: a confirmation link proves who reads the mail, not who set the account's password. Someone can
/// register with another person's address and a password of their own; when that person follows the "confirm your
/// account" mail, marking the address as proved would hand them over the guest orders placed with it. An account with
/// credentials is now marked by a link only when the same request is signed in to it with them.
/// </summary>
public sealed class EmailOwnershipConfirmationLinkTests : IAsyncLifetime
{
    private const string Email = "pat@example.test";

    private EmailSignInHarness _harness;

    public async ValueTask InitializeAsync()
        => _harness = await EmailSignInHarness.CreateAsync();

    public async ValueTask DisposeAsync()
        => await _harness.DisposeAsync();

    [Fact]
    public async Task ALinkFollowedWithoutTheCredentials_DoesNotMarkAnAccountThatHasThem()
    {
        // Arrange: someone registered with the address and a password of their own.
        await _harness.CreateUserAsync(new User { UserId = "user-1", UserName = "squatter", Email = Email, IsEnabled = true });

        // Act: the address's owner follows the link, not signed in.
        var account = await ConfirmAsync("user-1", principal: new ClaimsPrincipal(new ClaimsIdentity()));

        // Assert
        Assert.False(UserEmailOwnership.IsVerified(account.Properties, account.Email));
    }

    [Fact]
    public async Task ALinkFollowedByTheOwnerSignedInWithThePassword_MarksTheAccount()
    {
        // Arrange
        await _harness.CreateUserAsync(new User { UserId = "user-1", UserName = "pat", Email = Email, IsEnabled = true });

        // Act: a session a password began carries amr=pwd.
        var account = await ConfirmAsync("user-1", SignedIn("user-1", new Claim("amr", "pwd")));

        // Assert
        Assert.True(UserEmailOwnership.IsVerified(account.Properties, account.Email));
    }

    [Fact]
    public async Task ALinkFollowedSignedInWithoutAPasswordSession_DoesNotMarkAnAccountWithAPassword()
    {
        // Arrange: Orchard Core signs a new registrant in with no amr claim, as an e-mailed code does.
        await _harness.CreateUserAsync(new User { UserId = "user-1", UserName = "pat", Email = Email, IsEnabled = true });

        // Act
        var account = await ConfirmAsync("user-1", SignedIn("user-1"));

        // Assert
        Assert.False(UserEmailOwnership.IsVerified(account.Properties, account.Email));
    }

    [Fact]
    public async Task ALinkFollowedSignedInWithTheAccountsExternalLogin_MarksTheAccount()
    {
        // Arrange: an account that signs in with GitHub only.
        await _harness.CreateUserAsync(new User { UserId = "user-1", UserName = "pat", Email = Email, IsEnabled = true }, withPassword: false);

        // Act
        var account = await ConfirmAsync("user-1", SignedIn("user-1", new Claim(ClaimTypes.AuthenticationMethod, "GitHub")));

        // Assert
        Assert.True(UserEmailOwnership.IsVerified(account.Properties, account.Email));
    }

    [Fact]
    public async Task ALink_MarksAnAccountWithNoCredentials()
    {
        // Arrange: an account made from an order, which nobody could sign in to before.
        await _harness.CreateUserAsync(new User { UserId = "user-1", UserName = "pat", Email = Email, IsEnabled = true }, withPassword: false, withLogin: false);

        // Act
        var account = await ConfirmAsync("user-1", principal: new ClaimsPrincipal(new ClaimsIdentity()));

        // Assert
        Assert.True(UserEmailOwnership.IsVerified(account.Properties, account.Email));
    }

    private static ClaimsPrincipal SignedIn(string userId, params Claim[] claims)
        => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId), .. claims], "Identity.Application"));

    // Raises what Orchard Core raises after a confirmation link was followed, in a request with that user, and returns
    // the account as saved.
    private async Task<User> ConfirmAsync(string userId, ClaimsPrincipal principal)
    {
        await _harness.InRequestAsync(async services =>
        {
            var user = (User)await services.GetRequiredService<UserManager<IUser>>().FindByIdAsync(userId);
            var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal, RequestServices = services } };
            var handler = new EmailOwnershipConfirmationHandler(services.GetRequiredService<ISession>(), accessor, Mock.Of<IClock>(clock => clock.UtcNow == DateTime.UtcNow));

            await handler.ConfirmedAsync(new UserConfirmContext(user) { ConfirmationType = UserConfirmationType.Email });

            return true;
        });

        return await _harness.InRequestAsync(async services => (User)await services.GetRequiredService<UserManager<IUser>>().FindByIdAsync(userId));
    }
}

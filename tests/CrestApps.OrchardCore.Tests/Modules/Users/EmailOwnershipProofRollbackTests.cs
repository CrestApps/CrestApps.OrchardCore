using CrestApps.OrchardCore.Users;
using CrestApps.OrchardCore.Users.Controllers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Users;
using OrchardCore.Users.Handlers;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Tests.Modules.Users;

/// <summary>
/// MD1 (fix-verification review): the first proof of an address removed the password, then the external logins, then
/// rotated the stamp, each in an update of its own, and recorded the proof last. When that last update failed, the
/// user was told the code was wrong, the code was spent, and the earlier updates were committed with the request: the
/// password was gone without the notice. These run over Orchard Core's user store on SQLite, whose session commits
/// at the end of the request, with a user event handler that refuses the update recording the proof.
/// </summary>
public sealed class EmailOwnershipProofRollbackTests : IAsyncLifetime
{
    private const string Email = "pat@example.test";

    private EmailSignInHarness _harness;

    public async ValueTask InitializeAsync()
    {
        _harness = await EmailSignInHarness.CreateAsync();
        _harness.UserEventHandlers.Add(new RefuseTheProof());
    }

    public async ValueTask DisposeAsync()
        => await _harness.DisposeAsync();

    [Fact]
    public async Task WhenTheProofCantBeSaved_TheAccountKeepsEverythingItHad()
    {
        // Arrange: an account whose address was never proved, with a password and an external login.
        var stampBefore = await _harness.CreateUserAsync(new User { UserId = "user-1", UserName = "pat", Email = Email, IsEnabled = true });

        // Act: the right code, in one request that commits at its end.
        var result = await SignInWithCodeAsync();

        // Assert: refused, and nothing of the attempt was kept.
        Assert.IsType<ViewResult>(result);

        await _harness.InRequestAsync(async services =>
        {
            var manager = services.GetRequiredService<UserManager<IUser>>();
            var account = (User)await manager.FindByIdAsync("user-1");

            Assert.True(await manager.CheckPasswordAsync(account, EmailSignInHarness.Password), "A failed proof must not remove the password.");
            Assert.Single(await manager.GetLoginsAsync(account));
            Assert.Equal(stampBefore, account.SecurityStamp);
            Assert.False(UserEmailOwnership.IsVerified(account.Properties, account.Email));

            return true;
        });
    }

    private async Task<IActionResult> SignInWithCodeAsync()
    {
        await _harness.InRequestAsync(services => _harness.CreateController(services).Index(new EmailSignInViewModel { Email = Email }));
        var code = Assert.Single(_harness.Sender.Sent).Code;

        return await _harness.InRequestAsync(services => _harness.CreateController(services).Code(new EmailSignInViewModel { Email = Email, Code = code }));
    }

    // Refuses the update that records the address as proved, as any user event handler may refuse an update.
    private sealed class RefuseTheProof : UserEventHandlerBase
    {
        public override Task UpdatingAsync(UserUpdateContext context)
        {
            if (context.User is User user && user.Properties.ContainsKey(UserEmailOwnership.PropertyName))
            {
                context.Cancel = true;
            }

            return Task.CompletedTask;
        }
    }
}

using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Handlers;
using CrestApps.OrchardCore.Users;
using CrestApps.OrchardCore.Users.Controllers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OrchardCore.Users;
using OrchardCore.Users.Events;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Tests.Modules.Users;

/// <summary>
/// H3 (final security review): a correct e-mailed code marked the account's address proved and merged the guest orders
/// placed with it, but kept whatever let someone in before the address was proved. Someone who registered with a guest
/// buyer's address and their own password, or changed their email to it, then signed in with that password and read the
/// buyer's orders. Since MD1 a code alone hands nothing over to such an account; the owner confirms the address with a
/// second code, and that proof removes what was set before it, then merges. These run both controllers over Orchard
/// Core's user store on SQLite, with the Customers feature's merge handler.
/// </summary>
public sealed class EmailSignInFirstProofTests : IAsyncLifetime
{
    private const string Email = "pat@example.test";

    private readonly List<MergeSeen> _merges = [];

    private EmailSignInHarness _harness;

    public async ValueTask InitializeAsync()
        => _harness = await EmailSignInHarness.CreateAsync();

    public async ValueTask DisposeAsync()
        => await _harness.DisposeAsync();

    [Fact]
    public async Task AnUnconfirmedAccountRegisteredWithTheAddress_LosesItsPasswordAndLogins_BeforeTheGuestsMerge()
    {
        // Arrange: somebody registered with the guest buyer's address, their own password and an external login, and
        // never confirmed it. The site does not require confirmation, so the code signs the buyer in.
        var stampBefore = await _harness.CreateUserAsync(new User { UserId = "user-1", UserName = "squatter", Email = Email, IsEnabled = true });

        // Act: the guest buyer signs in with a code, then confirms the address is theirs.
        var result = await SignInThenConfirmAsync("user-1");

        // Assert
        Assert.IsType<LocalRedirectResult>(result);
        await AssertTheCodeHolderAloneOwnsItAsync("user-1", stampBefore);
    }

    [Fact]
    public async Task AnAccountWhoseEmailWasChangedToTheAddress_LosesItsPasswordAndLogins_BeforeTheGuestsMerge()
    {
        // Arrange: a confirmed account that proved its own address, then changed its email to the guest buyer's, as
        // Orchard Core's profile editor allows without a round trip.
        var user = new User { UserId = "user-2", UserName = "changer", Email = "changer@example.test", EmailConfirmed = true, IsEnabled = true };
        UserEmailOwnership.MarkVerified(user.Properties, user.Email, _harness.Now.AddDays(-30));
        user.Email = Email;
        var stampBefore = await _harness.CreateUserAsync(user);

        // Act
        var result = await SignInThenConfirmAsync("user-2");

        // Assert
        Assert.IsType<LocalRedirectResult>(result);
        await AssertTheCodeHolderAloneOwnsItAsync("user-2", stampBefore);
    }

    [Fact]
    public async Task AnAccountWhoseAddressWasAlreadyProved_KeepsItsPassword()
    {
        // Arrange: the code is not the first proof of the address, so nothing set before it is in doubt.
        var user = new User { UserId = "user-3", UserName = "pat", Email = Email, EmailConfirmed = true, IsEnabled = true };
        UserEmailOwnership.MarkVerified(user.Properties, Email, _harness.Now.AddDays(-30));
        var stampBefore = await _harness.CreateUserAsync(user);

        // Act
        var result = await SignInWithCodeAsync();

        // Assert
        Assert.IsType<LocalRedirectResult>(result);

        await _harness.InRequestAsync(async services =>
        {
            var manager = services.GetRequiredService<UserManager<IUser>>();
            var account = (User)await manager.FindByIdAsync("user-3");

            Assert.True(await manager.CheckPasswordAsync(account, EmailSignInHarness.Password));
            Assert.Single(await manager.GetLoginsAsync(account));
            Assert.Equal(stampBefore, account.SecurityStamp);

            return true;
        });
    }

    private async Task AssertTheCodeHolderAloneOwnsItAsync(string userId, string stampBefore)
    {
        await _harness.InRequestAsync(async services =>
        {
            var manager = services.GetRequiredService<UserManager<IUser>>();
            var account = (User)await manager.FindByIdAsync(userId);

            Assert.False(await manager.HasPasswordAsync(account), "A password set before the address was proved must not survive its proof.");
            Assert.False(await manager.CheckPasswordAsync(account, EmailSignInHarness.Password));
            Assert.Empty(await manager.GetLoginsAsync(account));
            Assert.NotEqual(stampBefore, account.SecurityStamp);
            Assert.True(account.EmailConfirmed);
            Assert.True(UserEmailOwnership.IsVerified(account.Properties, account.Email));

            return true;
        });

        // The guests merged once, and only once nothing set before the proof could sign in to the account.
        var merge = Assert.Single(_merges);
        Assert.Equal(userId, merge.UserId);
        Assert.False(merge.HadPassword);
        Assert.Equal(0, merge.Logins);
    }

    private async Task<IActionResult> SignInWithCodeAsync()
    {
        await _harness.InRequestAsync(services => _harness.CreateController(services).Index(new EmailSignInViewModel { Email = Email }));
        var code = _harness.Sender.Sent[^1].Code;

        return await _harness.InRequestAsync(services => _harness.CreateController(services, LoginFormEvents(services)).Code(new EmailSignInViewModel { Email = Email, Code = code }));
    }

    private async Task<IActionResult> SignInThenConfirmAsync(string userId)
    {
        Assert.IsType<LocalRedirectResult>(await SignInWithCodeAsync());
        Assert.Empty(_merges);

        await _harness.InRequestAsync(services => _harness.CreateConfirmController(services, userId).Confirm(new EmailSignInViewModel()));
        var code = _harness.Sender.Sent[^1].Code;

        return await _harness.InRequestAsync(services => _harness.CreateConfirmController(services, userId, LoginFormEvents(services)).ConfirmCode(new EmailSignInViewModel { Code = code }));
    }

    // The Customers feature's handler, resolving the merge service when the event fires, as in production.
    private ILoginFormEvent[] LoginFormEvents(IServiceProvider requestServices)
    {
        var mergeServices = new ServiceCollection()
            .AddSingleton<ICustomerMergeService>(new RecordingMergeService(requestServices.GetRequiredService<UserManager<IUser>>(), _merges))
            .BuildServiceProvider();

        return [new CustomerMergeLoginFormEvent(mergeServices, NullLogger<CustomerMergeLoginFormEvent>.Instance)];
    }

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
            var user = await _manager.FindByIdAsync(userId);

            _merges.Add(new MergeSeen(userId, await _manager.HasPasswordAsync(user), (await _manager.GetLoginsAsync(user)).Count));

            return 1;
        }
    }
}

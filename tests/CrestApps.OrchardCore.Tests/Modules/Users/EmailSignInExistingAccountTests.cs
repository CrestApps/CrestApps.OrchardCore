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

using System.Net;
using CrestApps.OrchardCore.Core.Throttling;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Handlers;
using CrestApps.OrchardCore.Tests.Utilities;
using CrestApps.OrchardCore.Throttling;
using CrestApps.OrchardCore.Users;
using CrestApps.OrchardCore.Users.Controllers;
using CrestApps.OrchardCore.Users.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking;
using OrchardCore.Modules;
using OrchardCore.Security.Services;
using OrchardCore.Settings;
using OrchardCore.Users;
using OrchardCore.Users.Events;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using OrchardCore.Users.Services;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Modules.Users;

/// <summary>
/// H3 (final security review): a correct e-mailed code marked the account's address proved and merged the guest orders
/// placed with it, but kept whatever let someone in before the address was proved. Someone who registered with a guest
/// buyer's address and their own password, or changed their email to it, then signed in with that password and read the
/// buyer's orders. These run the controller with ASP.NET Core Identity's user manager over Orchard Core's own user store
/// on SQLite, and the Customers feature's merge handler.
/// </summary>
public sealed class EmailSignInFirstProofTests : IAsyncLifetime
{
    private const string Email = "pat@example.test";
    private const string EarlierPassword = "Earlier-Passw0rd!";

    private readonly string _databasePath = Path.Combine(AppContext.BaseDirectory, $"email-sign-in-first-proof-{Guid.NewGuid():N}.db");
    private readonly IDistributedCache _cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
    private readonly LocalLock _lock = new(NullLogger<LocalLock>.Instance);
    private readonly List<MergeSeen> _merges = [];
    private readonly DateTime _now = DateTime.UtcNow;
    private IStore _store;
    private ServiceProvider _services;

    public async ValueTask InitializeAsync()
    {
        _store = await CreateStoreAsync(_databasePath);

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        services.AddIdentityCore<IUser>();
        services.AddScoped(_ => _store.CreateSession());
        services.AddScoped<IUserStore<IUser>>(provider => new UserStore(
            provider.GetRequiredService<ISession>(),
            provider.GetRequiredService<ILookupNormalizer>(),
            Mock.Of<IUserIdGenerator>(),
            NullLogger<UserStore>.Instance,
            [],
            Mock.Of<IRoleService>(),
            provider.GetRequiredService<IDataProtectionProvider>()));

        _services = services.BuildServiceProvider();
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);
    }

    [Fact]
    public async Task AnUnconfirmedAccountRegisteredWithTheAddress_LosesItsPasswordAndLogins_BeforeTheGuestsMerge()
    {
        // Arrange: somebody registered with the guest buyer's address, their own password and an external login, and
        // never confirmed it.
        var stampBefore = await CreateUserAsync(new User { UserId = "user-1", UserName = "squatter", Email = Email, IsEnabled = true });

        // Act: the guest buyer signs in with the code e-mailed to their address.
        var result = await SignInWithCodeAsync();

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
        UserEmailOwnership.MarkVerified(user.Properties, user.Email, _now.AddDays(-30));
        user.Email = Email;
        var stampBefore = await CreateUserAsync(user);

        // Act
        var result = await SignInWithCodeAsync();

        // Assert
        Assert.IsType<LocalRedirectResult>(result);
        await AssertTheCodeHolderAloneOwnsItAsync("user-2", stampBefore);
    }

    [Fact]
    public async Task AnAccountWhoseAddressWasAlreadyProved_KeepsItsPassword()
    {
        // Arrange: the code is not the first proof of the address, so nothing set before it is in doubt.
        var user = new User { UserId = "user-3", UserName = "pat", Email = Email, EmailConfirmed = true, IsEnabled = true };
        UserEmailOwnership.MarkVerified(user.Properties, Email, _now.AddDays(-30));
        var stampBefore = await CreateUserAsync(user);

        // Act
        var result = await SignInWithCodeAsync();

        // Assert
        Assert.IsType<LocalRedirectResult>(result);

        await using var scope = _services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<IUser>>();
        var account = (User)await manager.FindByIdAsync("user-3");

        Assert.True(await manager.CheckPasswordAsync(account, EarlierPassword));
        Assert.Single(await manager.GetLoginsAsync(account));
        Assert.Equal(stampBefore, account.SecurityStamp);
    }

    private async Task AssertTheCodeHolderAloneOwnsItAsync(string userId, string stampBefore)
    {
        await using var scope = _services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<IUser>>();
        var account = (User)await manager.FindByIdAsync(userId);

        Assert.False(await manager.HasPasswordAsync(account), "A password set before the address was proved must not survive its proof.");
        Assert.False(await manager.CheckPasswordAsync(account, EarlierPassword));
        Assert.Empty(await manager.GetLoginsAsync(account));
        Assert.NotEqual(stampBefore, account.SecurityStamp);
        Assert.True(account.EmailConfirmed);
        Assert.True(UserEmailOwnership.IsVerified(account.Properties, account.Email));

        // The guests merged, and only once nothing set before the proof could sign in to the account.
        var merge = Assert.Single(_merges);
        Assert.Equal(userId, merge.UserId);
        Assert.False(merge.HadPassword);
        Assert.Equal(0, merge.Logins);
    }

    // Creates the account with the earlier password and an external login, and returns its security stamp.
    private async Task<string> CreateUserAsync(User user)
    {
        await using var scope = _services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<IUser>>();

        user.LoginInfos.Add(new UserLoginInfo("GitHub", "github-" + user.UserId, "GitHub"));

        var created = await manager.CreateAsync(user, EarlierPassword);

        Assert.True(created.Succeeded, string.Join(", ", created.Errors.Select(error => error.Description)));
        await scope.ServiceProvider.GetRequiredService<ISession>().SaveChangesAsync(TestContext.Current.CancellationToken);

        return user.SecurityStamp;
    }

    // One request: the code is issued, then posted, then the request's session commits.
    private async Task<IActionResult> SignInWithCodeAsync()
    {
        await using var scope = _services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<IUser>>();
        var code = await CreateCodes().IssueAsync(Email, TestContext.Current.CancellationToken);
        var result = await CreateController(manager, scope.ServiceProvider).Code(new EmailSignInViewModel { Email = Email, Code = code });

        await scope.ServiceProvider.GetRequiredService<ISession>().SaveChangesAsync(TestContext.Current.CancellationToken);

        return result;
    }

    private EmailSignInCodes CreateCodes()
    {
        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(() => _now);

        var options = new AttemptLimitOptions()
            .AddPolicy(EmailSignInCodes.SendNetworkPolicy, 10, TimeSpan.FromMinutes(15))
            .AddPolicy(EmailSignInCodes.SendEmailPolicy, 3, TimeSpan.FromMinutes(15))
            .AddPolicy(EmailSignInCodes.VerifyNetworkPolicy, 30, TimeSpan.FromMinutes(15));
        var limiter = new DistributedAttemptLimiter(_cache, _lock, new ShellSettings { Name = "Default" }, clock.Object, Options.Create(options), NullLogger<DistributedAttemptLimiter>.Instance);

        return new EmailSignInCodes(_cache, limiter, new ShellSettings { Name = "Default" }, clock.Object, _lock);
    }

    private EmailSignInController CreateController(UserManager<IUser> manager, IServiceProvider requestServices)
    {
        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(() => _now);

        var site = new SiteSettings { SiteName = "Test site" };
        var siteService = new Mock<ISiteService>();
        siteService.Setup(s => s.GetSiteSettingsAsync()).ReturnsAsync(site);

        var signInManager = new Mock<SignInManager<IUser>>(manager, Mock.Of<IHttpContextAccessor>(), Mock.Of<IUserClaimsPrincipalFactory<IUser>>(), null, null, null, null);
        signInManager.Setup(s => s.SignInAsync(It.IsAny<IUser>(), It.IsAny<bool>(), It.IsAny<string>())).Returns(Task.CompletedTask);

        // The Customers feature's handler, resolving the merge service when the event fires, as in production.
        var mergeServices = new ServiceCollection()
            .AddSingleton<ICustomerMergeService>(new RecordingMergeService(manager, _merges))
            .BuildServiceProvider();
        ILoginFormEvent[] loginFormEvents = [new CustomerMergeLoginFormEvent(mergeServices, NullLogger<CustomerMergeLoginFormEvent>.Instance)];

        var controller = new EmailSignInController(
            manager,
            signInManager.Object,
            CreateCodes(),
            Mock.Of<IEmailSignInCodeSender>(),
            siteService.Object,
            loginFormEvents,
            clock.Object,
            Mock.Of<global::OrchardCore.DisplayManagement.Notify.INotifier>(),
            Options.Create(new EmailSignInOptions()),
            NullLogger<EmailSignInController>.Instance,
            new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<EmailSignInController>(),
            Mock.Of<Microsoft.AspNetCore.Mvc.Localization.IHtmlLocalizer<EmailSignInController>>());

        var httpContext = new DefaultHttpContext { RequestServices = requestServices };
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");

        var url = new Mock<IUrlHelper>();
        url.Setup(u => u.IsLocalUrl(It.IsAny<string>())).Returns(true);
        url.Setup(u => u.Content(It.IsAny<string>())).Returns("/");

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.Url = url.Object;

        return controller;
    }

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes([new UserIndexProvider()]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

        // Orchard Core's Users migration creates the same columns.
        await schemaBuilder.CreateMapIndexTableAsync<UserIndex>(table => table
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<string>("NormalizedUserName", column => column.WithLength(255))
            .Column<string>("NormalizedEmail", column => column.WithLength(255))
            .Column<bool>("IsEnabled")
            .Column<bool>("IsLockoutEnabled")
            .Column<int>("AccessFailedCount")
            .Column<DateTime>("LockoutEndUtc"));

        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }

    private sealed record MergeSeen(string UserId, bool HadPassword, int Logins);

    // Records what the account could be signed in with when its guests were merged into it.
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

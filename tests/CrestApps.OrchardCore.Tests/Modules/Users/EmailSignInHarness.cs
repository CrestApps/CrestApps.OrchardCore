using System.Net;
using System.Security.Claims;
using CrestApps.OrchardCore.Core.Throttling;
using CrestApps.OrchardCore.Tests.Utilities;
using CrestApps.OrchardCore.Throttling;
using CrestApps.OrchardCore.Users.Controllers;
using CrestApps.OrchardCore.Users.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
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
using OrchardCore.Users.Handlers;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using OrchardCore.Users.Services;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Modules.Users;

/// <summary>
/// E-mail code sign-in over the real parts: ASP.NET Core Identity's user manager over Orchard Core's own user store on
/// SQLite, the real code store and attempt limiter, and a sender that records what would be mailed. The user index's
/// <c>NormalizedEmail</c> column compares like SQL Server's default collation (see
/// <see cref="LooseCollationConnectionFactory"/>), so a look-alike address finds another account as it does there.
/// </summary>
internal sealed class EmailSignInHarness : IAsyncDisposable
{
    public const string Password = "Earlier-Passw0rd!";

    private readonly string _databasePath = Path.Combine(AppContext.BaseDirectory, $"email-sign-in-{Guid.NewGuid():N}.db");
    private readonly LocalLock _lock = new(NullLogger<LocalLock>.Instance);

    private readonly Action<IServiceCollection> _configureServices;

    private EmailSignInHarness(Action<IServiceCollection> configureServices)
    {
        _configureServices = configureServices;
    }

    public IStore Store { get; private set; }

    public ServiceProvider Services { get; private set; }

    public IDistributedCache Cache => RecordingCache;

    /// <summary>
    /// The harness's cache, which records each read, write and removal ("Get", "Set", "Remove") in the order made.
    /// </summary>
    public RecordingDistributedCache RecordingCache { get; } = new(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));

    public DateTime Now { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the address the next requests come from.
    /// </summary>
    public string RemoteAddress { get; set; } = "203.0.113.7";

    public RecordingCodeSender Sender { get; } = new();

    /// <summary>
    /// The user ids whose OpenID tokens and other issued sign-ins were revoked.
    /// </summary>
    public List<string> Revoked { get; } = [];

    /// <summary>
    /// The sign-ins the controllers made, with the claims each added (none for a plain sign-in).
    /// </summary>
    public List<SignIn> SignIns { get; } = [];

    /// <summary>
    /// Orchard Core's registration settings, read as the site's settings are.
    /// </summary>
    public RegistrationOptions Registration { get; } = new();

    public UserCredentialsResets CredentialsResets => new(Cache, new ShellSettings { Name = "Default" }, Options.Create(new SecurityStampValidatorOptions()), NullLogger<UserCredentialsResets>.Instance);

    /// <summary>
    /// The user event handlers Orchard Core's user store raises its events on.
    /// </summary>
    public List<IUserEventHandler> UserEventHandlers { get; } = [];

    /// <summary>
    /// What the external provider returns when a confirmation page signs in through it again (K5), as Identity reads it
    /// from the external cookie, with the account id the challenge was bound to; or <see langword="null"/>.
    /// </summary>
    public (ExternalLoginInfo Info, string BoundTo)? ExternalLogin { get; set; }

    private string ConnectionString => $"Data Source={_databasePath};Pooling=False";

    /// <summary>
    /// Creates the harness. <paramref name="configureServices"/> adds the services a feature registers, as its startup
    /// does; the user event handlers it registers are raised by the user store.
    /// </summary>
    public static async Task<EmailSignInHarness> CreateAsync(Action<IServiceCollection> configureServices = null)
    {
        var harness = new EmailSignInHarness(configureServices);
        await harness.InitializeAsync();

        return harness;
    }

    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync();
        TemporarySqliteDatabase.DisposeAndDelete(Store, _databasePath);
    }

    /// <summary>
    /// Creates the account, with the earlier password and an external login when asked, and returns its security stamp.
    /// </summary>
    public async Task<string> CreateUserAsync(User user, bool withPassword = true, bool withLogin = true)
    {
        await using var scope = Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<IUser>>();

        if (withLogin)
        {
            user.LoginInfos.Add(new UserLoginInfo("GitHub", "github-" + user.UserId, "GitHub"));
        }

        var created = withPassword ? await manager.CreateAsync(user, Password) : await manager.CreateAsync(user);

        Assert.True(created.Succeeded, string.Join(", ", created.Errors.Select(error => error.Description)));
        await scope.ServiceProvider.GetRequiredService<ISession>().SaveChangesAsync(TestContext.Current.CancellationToken);

        return user.SecurityStamp;
    }

    /// <summary>
    /// Registers a passkey on the account through ASP.NET Core Identity's user manager, as Orchard Core's passkey page
    /// does once the browser's attestation is verified, in a request signed in as <paramref name="session"/> when given.
    /// Returns the passkey's credential id.
    /// </summary>
    public Task<byte[]> AddPasskeyAsync(string userId, byte id = 1, ClaimsPrincipal session = null)
        => InRequestAsync(async services =>
        {
            if (session is not null)
            {
                services.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext { User = session };
            }

            var manager = services.GetRequiredService<UserManager<IUser>>();
            var credentialId = new byte[] { 0x50, 0x4b, id };
            var passkey = new UserPasskeyInfo(credentialId, [1, 2, 3], new DateTimeOffset(Now), 0, ["internal"], isUserVerified: true, isBackupEligible: false, isBackedUp: false, [4], [5])
            {
                Name = "Passkey " + id,
            };
            var added = await manager.AddOrUpdatePasskeyAsync(await manager.FindByIdAsync(userId), passkey);

            Assert.True(added.Succeeded, string.Join(", ", added.Errors.Select(error => error.Description)));

            return credentialId;
        });

    /// <summary>
    /// Removes a passkey of the account through ASP.NET Core Identity's user manager, as Orchard Core's passkey page does,
    /// in a request signed in as <paramref name="session"/> when given.
    /// </summary>
    public Task<IdentityResult> RemovePasskeyAsync(string userId, byte[] credentialId, ClaimsPrincipal session = null)
        => InRequestAsync(async services =>
        {
            if (session is not null)
            {
                services.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext { User = session };
            }

            var manager = services.GetRequiredService<UserManager<IUser>>();

            return await manager.RemovePasskeyAsync(await manager.FindByIdAsync(userId), credentialId);
        });

    /// <summary>
    /// The claims a sign-in to the account puts in its cookie now, after the user id: what Orchard Core's claims
    /// principal factory makes of the account as saved (its security stamp included), and the claims the sign-in adds
    /// (<c>amr=pwd</c> for a password, the provider's name for an external login, <c>amr=otp</c> for an e-mailed code).
    /// </summary>
    public Task<Claim[]> SignInClaimsAsync(string userId, params Claim[] signInClaims)
        => InRequestAsync(async services =>
        {
            var account = await services.GetRequiredService<UserManager<IUser>>().FindByIdAsync(userId);
            var principal = await services.GetRequiredService<IUserClaimsPrincipalFactory<IUser>>().CreateAsync(account);

            return (Claim[])[.. principal.Claims.Where(claim => claim.Type != ClaimTypes.NameIdentifier), .. signInClaims];
        });

    /// <summary>
    /// Runs one request:the action gets the request's services, then the request's session commits.
    /// </summary>
    public async Task<T> InRequestAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        var result = await action(scope.ServiceProvider);

        await scope.ServiceProvider.GetRequiredService<ISession>().SaveChangesAsync(TestContext.Current.CancellationToken);

        return result;
    }

    /// <summary>
    /// The user event handlers of a request: those registered, then <see cref="UserEventHandlers"/>.
    /// </summary>
    public IUserEventHandler[] GetUserEventHandlers(IServiceProvider requestServices)
        => [.. requestServices.GetServices<IUserEventHandler>(), .. UserEventHandlers];

    public EmailSignInCodes CreateCodes()
    {
        var clock = CreateClock();

        var options = new AttemptLimitOptions()
            .AddPolicy(EmailSignInCodes.SendNetworkPolicy, 10, TimeSpan.FromMinutes(15))
            .AddPolicy(EmailSignInCodes.SendEmailPolicy, 3, TimeSpan.FromMinutes(15))
            .AddPolicy(EmailSignInCodes.SendAccountPolicy, 10, TimeSpan.FromDays(1))
            .AddPolicy(EmailSignInCodes.SendAccountTotalPolicy, 50, TimeSpan.FromDays(1))
            .AddPolicy(EmailSignInCodes.VerifyNetworkPolicy, 30, TimeSpan.FromMinutes(15))
            .AddPolicy(EmailSignInCodes.VerifyAccountPolicy, 10, TimeSpan.FromDays(1), TimeSpan.FromDays(1));
        var limiter = new DistributedAttemptLimiter(Cache, _lock, new ShellSettings { Name = "Default" }, clock, Options.Create(options), NullLogger<DistributedAttemptLimiter>.Instance);

        return new EmailSignInCodes(Cache, limiter, new ShellSettings { Name = "Default" }, clock, _lock);
    }

    public EmailSignInController CreateController(IServiceProvider requestServices, IEnumerable<ILoginFormEvent> loginFormEvents = null)
    {
        var manager = requestServices.GetRequiredService<UserManager<IUser>>();

        var controller = new EmailSignInController(
            manager,
            CreateSignInManager(manager),
            CreateCodes(),
            Sender,
            CreateSiteService(),
            loginFormEvents ?? [],
            CreateProof(requestServices),
            Mock.Of<global::OrchardCore.DisplayManagement.Notify.INotifier>(),
            new EmailSignInEligibility(manager, SystemRoles(), Options.Create(new EmailSignInOptions())),
            NullLogger<EmailSignInController>.Instance,
            new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<EmailSignInController>(),
            Mock.Of<Microsoft.AspNetCore.Mvc.Localization.IHtmlLocalizer<EmailSignInController>>());

        return Prepare(controller, requestServices, signedInUserId: null);
    }

    /// <summary>
    /// The confirmation pages, for a request signed in as <paramref name="userId"/>, with the claims a sign-in added
    /// (none for an e-mailed code; <c>amr=pwd</c> for a password).
    /// </summary>
    public EmailOwnershipController CreateConfirmController(IServiceProvider requestServices, string userId, IEnumerable<ILoginFormEvent> loginFormEvents = null, params Claim[] signInClaims)
    {
        var manager = requestServices.GetRequiredService<UserManager<IUser>>();

        var controller = new EmailOwnershipController(
            manager,
            CreateSignInManager(manager),
            CreateCodes(),
            Sender,
            CreateProof(requestServices),
            new EmailSignInEligibility(manager, SystemRoles(), Options.Create(new EmailSignInOptions())),
            CreateSiteService(),
            loginFormEvents ?? [],
            Mock.Of<global::OrchardCore.DisplayManagement.Notify.INotifier>(),
            NullLogger<EmailOwnershipController>.Instance,
            new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<EmailOwnershipController>(),
            Mock.Of<Microsoft.AspNetCore.Mvc.Localization.IHtmlLocalizer<EmailOwnershipController>>());

        return Prepare(controller, requestServices, userId, signInClaims);
    }

    /// <summary>
    /// The proof service a request uses.
    /// </summary>
    public EmailOwnershipProof CreateProof(IServiceProvider requestServices, UserCredentialsResets credentialsResets = null)
        => new(
            requestServices.GetRequiredService<UserManager<IUser>>(),
            requestServices.GetRequiredService<ISession>(),
            credentialsResets ?? CredentialsResets,
            [new RecordingRevocation(Revoked)],
            new CrestApps.OrchardCore.Tests.Telephony.Doubles.TestOptionsMonitor<RegistrationOptions>(Registration),
            CreateClock(),
            new CrestApps.OrchardCore.Core.Services.SignedTokenService(requestServices.GetRequiredService<IDataProtectionProvider>(), CreateClock()),
            NullLogger<EmailOwnershipProof>.Instance);

    private SignInManager<IUser> CreateSignInManager(UserManager<IUser> manager)
    {
        var signInManager = new Mock<SignInManager<IUser>>(manager, Mock.Of<IHttpContextAccessor>(), Mock.Of<IUserClaimsPrincipalFactory<IUser>>(), null, null, null, null);
        signInManager.Setup(s => s.SignInAsync(It.IsAny<IUser>(), It.IsAny<bool>(), It.IsAny<string>()))
            .Callback((IUser user, bool _, string method) => SignIns.Add(new SignIn(user, method is null ? [] : [new Claim(ClaimTypes.AuthenticationMethod, method)])))
            .Returns(Task.CompletedTask);
        signInManager.Setup(s => s.SignInWithClaimsAsync(It.IsAny<IUser>(), It.IsAny<bool>(), It.IsAny<IEnumerable<Claim>>()))
            .Callback((IUser user, bool _, IEnumerable<Claim> claims) => SignIns.Add(new SignIn(user, [.. claims])))
            .Returns(Task.CompletedTask);

        // Identity refuses an external cookie whose challenge was bound to another account (its XSRF check).
        signInManager.Setup(s => s.GetExternalLoginInfoAsync(It.IsAny<string>()))
            .ReturnsAsync((string expectedXsrf) => ExternalLogin is { } login && login.BoundTo == expectedXsrf ? login.Info : null);

        return signInManager.Object;
    }

    /// <summary>
    /// Orchard Core's system role provider for a tenant whose administrator role is <paramref name="adminRoleName"/>.
    /// </summary>
    public static global::OrchardCore.Roles.ISystemRoleProvider SystemRoles(string adminRoleName = "Administrator")
        => new global::OrchardCore.Roles.DefaultSystemRoleProvider(
            new ShellSettings { Name = "Default" },
            new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<global::OrchardCore.Roles.DefaultSystemRoleProvider>(),
            Options.Create(new global::OrchardCore.Roles.SystemRoleOptions { SystemAdminRoleName = adminRoleName }));

    public static ISiteService CreateSiteService()
    {
        var siteService = new Mock<ISiteService>();
        siteService.Setup(s => s.GetSiteSettingsAsync()).ReturnsAsync(new SiteSettings { SiteName = "Test site" });

        return siteService.Object;
    }

    private T Prepare<T>(T controller, IServiceProvider requestServices, string signedInUserId, params Claim[] signInClaims)
        where T : Controller
    {
        var httpContext = new DefaultHttpContext { RequestServices = requestServices };
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse(RemoteAddress);

        if (signedInUserId is not null)
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, signedInUserId), .. signInClaims], "Identity.Application"));
        }

        var url = new Mock<IUrlHelper>();
        url.Setup(u => u.IsLocalUrl(It.IsAny<string>())).Returns(true);
        url.Setup(u => u.Content(It.IsAny<string>())).Returns("/");

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
        controller.Url = url.Object;

        return controller;
    }

    private IClock CreateClock()
    {
        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(() => Now);

        return clock.Object;
    }

    private async Task InitializeAsync()
    {
        Store = StoreFactory.Create(configuration =>
        {
            configuration.UseSqLite(ConnectionString);
            configuration.ConnectionFactory = new LooseCollationConnectionFactory(ConnectionString);
        });
        Store.RegisterIndexes([new UserIndexProvider(), new UserByLoginInfoIndexProvider()]);
        await Store.InitializeAsync(TestContext.Current.CancellationToken);

        await using (var session = Store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var schemaBuilder = new SchemaBuilder(Store.Configuration, transaction);

            // Orchard Core's Users migration creates the same columns.
            await schemaBuilder.CreateMapIndexTableAsync<UserIndex>(table => table
                .Column<string>("UserId", column => column.WithLength(26))
                .Column<string>("NormalizedUserName", column => column.WithLength(255))
                .Column<string>("NormalizedEmail", column => column.WithLength(255))
                .Column<bool>("IsEnabled")
                .Column<bool>("IsLockoutEnabled")
                .Column<int>("AccessFailedCount")
                .Column<DateTime>("LockoutEndUtc"));

            // Identity's AddLoginAsync looks the login up in it, as Orchard Core's migration creates it.
            await schemaBuilder.CreateMapIndexTableAsync<UserByLoginInfoIndex>(table => table
                .Column<string>("LoginProvider")
                .Column<string>("ProviderKey"));

            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        await LooseCollationConnectionFactory.UseLooseCollationAsync(ConnectionString, nameof(UserIndex), nameof(UserIndex.NormalizedEmail));

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddHttpContextAccessor();

        // The confirmation page signs out of the external cookie after a fresh sign-in through a login (K5).
        services.AddSingleton(Mock.Of<Microsoft.AspNetCore.Authentication.IAuthenticationService>());
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        services.AddIdentityCore<IUser>();

        // Orchard Core's Users feature builds the cookie's claims this way, with the IUserClaimsProvider services.
        services.AddScoped<IUserClaimsPrincipalFactory<IUser>, DefaultUserClaimsPrincipalProviderFactory>();
        services.AddSingleton(CreateClock());
        services.AddScoped(_ => Store.CreateSession());

        // The feature's resets and claims refreshes, over the harness's cache, as CredentialsResets reads them. The
        // feature's startup registers the service itself; this is what it needs.
        services.AddSingleton(Cache);
        services.AddSingleton(new ShellSettings { Name = "Default" });
        services.AddSingleton<UserCredentialsResets>();

        // Orchard Core's Users feature registers its e-mail confirmation token this way.
        services.AddOptions<EmailConfirmationTokenProviderOptions>();
        services.AddTransient<EmailConfirmationTokenProvider>();
        services.AddTransient<IConfigureOptions<IdentityOptions>, EmailConfirmationIdentityOptionsConfigurations>();
        services.AddScoped<IUserStore<IUser>>(provider => new UserStore(
            provider.GetRequiredService<ISession>(),
            provider.GetRequiredService<ILookupNormalizer>(),
            Mock.Of<IUserIdGenerator>(),
            NullLogger<UserStore>.Instance,
            GetUserEventHandlers(provider),
            Mock.Of<IRoleService>(),
            provider.GetRequiredService<IDataProtectionProvider>()));

        _configureServices?.Invoke(services);

        Services = services.BuildServiceProvider();
    }

    public sealed record SentCode(string UserName, string Email, string Code);

    public sealed record SignIn(IUser User, IReadOnlyList<Claim> Claims);

    private sealed class RecordingRevocation : IUserSignInRevocation
    {
        private readonly List<string> _revoked;

        public RecordingRevocation(List<string> revoked)
            => _revoked = revoked;

        public Task RevokeAsync(string userId, CancellationToken cancellationToken = default)
        {
            _revoked.Add(userId);

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Records the codes that would be mailed, and to which address.
    /// </summary>
    public sealed class RecordingCodeSender : IEmailSignInCodeSender
    {
        public List<SentCode> Sent { get; } = [];

        public Task QueueAsync(string userName, string email, string code, CancellationToken cancellationToken = default)
        {
            Sent.Add(new SentCode(userName, email, code));

            return Task.CompletedTask;
        }

        public Task SendAsync(string userName, string email, string code, CancellationToken cancellationToken = default)
            => QueueAsync(userName, email, code, cancellationToken);
    }
}

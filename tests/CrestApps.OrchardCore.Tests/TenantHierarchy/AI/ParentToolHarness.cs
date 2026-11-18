using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Tests.Utilities;
using CrestApps.OrchardCore.TenantHierarchy;
using CrestApps.OrchardCore.TenantHierarchy.Core;
using CrestApps.OrchardCore.TenantHierarchy.Core.Indexes;
using CrestApps.OrchardCore.TenantHierarchy.Core.Provisioning;
using CrestApps.OrchardCore.TenantHierarchy.Core.Services;
using CrestApps.OrchardCore.TenantHierarchy.Migrations;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Builders;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Recipes.Models;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;
using OrchardCore.Security.Services;
using OrchardCore.Setup.Services;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using IYesSqlSession = YesSql.ISession;
using IYesSqlStore = YesSql.IStore;
using YesSqlStoreFactory = YesSql.StoreFactory;

namespace CrestApps.OrchardCore.Tests.TenantHierarchy.AI;

/// <summary>
/// Runs the parent-side tools on the real <see cref="ChildTenantManager"/> and <see cref="AccessGrantManager"/>, with the
/// registry, the grants and the sessions in a SQLite database the module's migration creates. The broker, which touches
/// other tenants, is a double that keeps a runtime state per entry and records what it was asked to do.
/// </summary>
internal sealed class ParentToolHarness : IAsyncDisposable
{
    private readonly string _databasePath;
    private readonly IYesSqlStore _store;

    private ParentToolHarness(string databasePath, IYesSqlStore store, IYesSqlSession session, ShellSettings settings)
    {
        _databasePath = databasePath;
        _store = store;
        Session = session;
        Settings = settings;
        EntryStore = new ChildTenantEntryStore(session);
    }

    public IYesSqlSession Session { get; }

    public ShellSettings Settings { get; }

    public ChildTenantEntryStore EntryStore { get; }

    public Dictionary<string, ChildTenantRuntimeState> States { get; } = new(StringComparer.Ordinal);

    public List<string> BrokerCalls { get; } = [];

    public List<ChildFeatureInfo> Features { get; } = [];

    public static async Task<ParentToolHarness> CreateAsync(Action<ParentTenantPolicy> configurePolicy = null)
    {
        var settings = TestShellSettings.Parent("firm");
        var policy = new ParentTenantPolicy
        {
            MaxChildren = 5,
            Recipes = ["Retail"],
        };

        configurePolicy?.Invoke(policy);
        TenantHierarchySettingsWriter.WritePolicy(settings, policy);

        var databasePath = Path.Combine(Path.GetTempPath(), $"tenant-hierarchy-tools-{Guid.NewGuid():N}.db");
        var store = YesSqlStoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));

        store.RegisterIndexes(
        [
            new ChildTenantEntryIndexProvider(),
            new AccessGrantIndexProvider(),
            new DelegatedAccessCodeIndexProvider(),
            new DelegatedAccessSessionIndexProvider(),
            new TenantSwitcherPreferenceIndexProvider(),
        ]);

        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(TenantHierarchyConstants.CollectionName, TestContext.Current.CancellationToken);

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(new DateTime(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc));

        await using (var setupSession = store.CreateSession())
        {
            var transaction = await setupSession.BeginTransactionAsync(TestContext.Current.CancellationToken);

            // Only the grant store and the clock are used to seed the default grant.
            var grantManager = new AccessGrantManager(new AccessGrantStore(setupSession), new ChildTenantEntryStore(setupSession), null, null, null, null, clock.Object, null);
            var migration = new ParentTenantMigrations(grantManager)
            {
                SchemaBuilder = new SchemaBuilder(store.Configuration, transaction),
            };

            await migration.CreateAsync();
            await setupSession.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return new ParentToolHarness(databasePath, store, store.CreateSession(), settings);
    }

    public async Task<ChildTenantEntry> AddChildAsync(string slug, ChildTenantStatus status = ChildTenantStatus.Ready, ChildTenantRuntimeState state = ChildTenantRuntimeState.Running)
    {
        var entry = new ChildTenantEntry
        {
            EntryId = $"entry-{slug}",
            TenantId = $"tenant-{slug}",
            TenantName = $"t{slug.Replace("-", string.Empty, StringComparison.Ordinal)}",
            Slug = slug,
            DisplayName = char.ToUpperInvariant(slug[0]) + slug[1..],
            Host = $"{slug}.firm.platform.com",
            RecipeName = "Retail",
            Status = status,
            CreatedUtc = new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc),
        };

        await EntryStore.SaveAsync(entry);
        await EntryStore.SaveChangesAsync();
        States[entry.EntryId] = state;

        return entry;
    }

    public ServiceProvider Build(bool signedIn = true, Func<Permission, object, bool> authorize = null, ShellSettings settings = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);

        services.AddLogging();
        services.AddLocalization();
        services.AddOptions();
        services.AddSingleton(settings ?? Settings);
        services.AddSingleton(Session);
        services.AddSingleton<ChildTenantEntryStore>();
        services.AddSingleton<AccessGrantStore>();
        services.AddSingleton<DelegatedAccessSessionStore>();
        services.AddSingleton<HierarchyAuditLog>();
        services.AddSingleton<HierarchyLabelsProvider>();
        services.AddSingleton<ChildTenantManager>();
        services.AddSingleton<AccessGrantManager>();
        services.AddSingleton<ChildDatabaseProvisioning>();
        services.AddSingleton<IChildTenantDatabaseProvisioner, FakeProvisioner>();
        services.AddSingleton(new TenantHierarchyHostMarker());
        services.AddSingleton(CreateBroker());
        services.AddSingleton(CreateLock());
        services.AddSingleton(CreateSetupService());
        services.AddSingleton(CreateUserManager());
        services.AddSingleton(CreateRoleService());

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(new DateTime(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc));
        services.AddSingleton(clock.Object);

        // A deferred task asks the shell host for a new scope; without one it falls back to a scope of the same shell.
        var shellHost = new Mock<IShellHost>();
        shellHost.Setup(host => host.GetScopeAsync(It.IsAny<ShellSettings>())).ThrowsAsync(new InvalidOperationException("No shell host in tests."));
        services.AddSingleton(shellHost.Object);

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Runs work inside a shell scope of the parent, as a chat hub call does, so work deferred on it runs when it ends.
    /// </summary>
    public static async Task<T> InShellScopeAsync<T>(ServiceProvider provider, Func<IServiceProvider, Task<T>> work)
    {
        var context = new ShellContext
        {
            Settings = provider.GetRequiredService<ShellSettings>(),
            ServiceProvider = provider,
        };

        var result = default(T);

        await new ShellScope(context).UsingAsync(async scope => result = await work(scope.ServiceProvider), activateShell: false);

        return result;
    }

    public async ValueTask DisposeAsync()
    {
        await Session.DisposeAsync();
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);
    }

    private ChildTenantInfo ToInfo(ChildTenantEntry entry)
        => entry is null
            ? null
            : new ChildTenantInfo
            {
                Entry = entry,
                State = States.TryGetValue(entry.EntryId, out var state)
                    ? state
                    : entry.Status == ChildTenantStatus.Ready ? ChildTenantRuntimeState.Running : ChildTenantRuntimeState.Uninitialized,
                Address = $"https://{entry.Host}",
            };

    private ITenantHierarchyBroker CreateBroker()
    {
        var broker = new Mock<ITenantHierarchyBroker>();

        broker.Setup(value => value.ListChildrenAsync())
            .Returns(async () => (IReadOnlyList<ChildTenantInfo>)(await EntryStore.ListAsync()).Select(ToInfo).ToList());
        broker.Setup(value => value.GetChildAsync(It.IsAny<string>()))
            .Returns((string id) => GetChildAsync(id));
        broker.Setup(value => value.IsHostInUseAsync(It.IsAny<string>()))
            .ReturnsAsync((string host) => host.StartsWith("taken.", StringComparison.Ordinal));
        broker.Setup(value => value.CreateChildSettingsAsync(It.IsAny<string>(), It.IsAny<ChildDatabaseProvisioningResult>()))
            .Returns(async (string id, ChildDatabaseProvisioningResult _) =>
            {
                BrokerCalls.Add($"create:{id}");
                var entry = await EntryStore.FindByEntryIdAsync(id);
                entry.TenantId = $"tenant-{id}";
                await EntryStore.SaveAsync(entry);
                States[id] = ChildTenantRuntimeState.Uninitialized;

                return TenantHierarchyResult.Success;
            });
        broker.Setup(value => value.SetupChildAsync(It.IsAny<string>()))
            .Returns(async (string id) =>
            {
                BrokerCalls.Add($"setup:{id}");
                var entry = await EntryStore.FindByEntryIdAsync(id);
                entry.Status = ChildTenantStatus.Ready;
                await EntryStore.SaveAsync(entry);
                States[id] = ChildTenantRuntimeState.Running;

                return TenantHierarchyResult.Success;
            });
        broker.Setup(value => value.UpdateChildSettingsAsync(It.IsAny<string>()))
            .ReturnsAsync((string id) => Record($"update:{id}"));
        broker.Setup(value => value.SuspendChildAsync(It.IsAny<string>()))
            .ReturnsAsync((string id) => ChangeState(id, "suspend", ChildTenantRuntimeState.Running, ChildTenantRuntimeState.Suspended));
        broker.Setup(value => value.ResumeChildAsync(It.IsAny<string>()))
            .ReturnsAsync((string id) => ChangeState(id, "resume", ChildTenantRuntimeState.Suspended, ChildTenantRuntimeState.Running));
        broker.Setup(value => value.ReloadChildAsync(It.IsAny<string>()))
            .ReturnsAsync((string id) => Record($"reload:{id}"));
        broker.Setup(value => value.RemoveChildAsync(It.IsAny<string>()))
            .ReturnsAsync((string id) => Record($"remove:{id}"));
        broker.Setup(value => value.GetChildFeaturesAsync(It.IsAny<string>()))
            .ReturnsAsync(() => Features);
        broker.Setup(value => value.SetChildFeaturesAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<bool>()))
            .ReturnsAsync((string id, IEnumerable<string> ids, bool enable) => Record($"{(enable ? "enable" : "disable")}:{id}:{string.Join(",", ids)}"));
        broker.Setup(value => value.GetChildRolesAsync(It.IsAny<string>()))
            .ReturnsAsync(["Administrator", "Author", "Editor"]);

        return broker.Object;
    }

    private async Task<ChildTenantInfo> GetChildAsync(string id)
        => ToInfo(await EntryStore.FindByEntryIdAsync(id));

    private TenantHierarchyResult Record(string call)
    {
        BrokerCalls.Add(call);

        return TenantHierarchyResult.Success;
    }

    private TenantHierarchyResult ChangeState(string id, string call, ChildTenantRuntimeState from, ChildTenantRuntimeState to)
    {
        if (!States.TryGetValue(id, out var state) || state != from)
        {
            return TenantHierarchyResult.Failure($"Cannot {call} the child tenant in its state.");
        }

        States[id] = to;

        return Record($"{call}:{id}");
    }

    private static IDistributedLock CreateLock()
    {
        var locker = new Mock<ILocker>();
        var distributedLock = new Mock<IDistributedLock>();

        distributedLock.Setup(value => value.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync((locker.Object, true));

        return distributedLock.Object;
    }

    private static ISetupService CreateSetupService()
    {
        var setupService = new Mock<ISetupService>();

        setupService.Setup(value => value.GetSetupRecipesAsync()).ReturnsAsync(
        [
            new RecipeDescriptor { Name = "Retail", DisplayName = "Retail store", Description = "A shop." },
            new RecipeDescriptor { Name = "Blog", DisplayName = "Blog", Description = "A blog." },
        ]);

        return setupService.Object;
    }

    private static UserManager<IUser> CreateUserManager()
    {
        var userStore = new Mock<IUserStore<IUser>>();
        var userManager = new Mock<UserManager<IUser>>(userStore.Object, null, null, null, null, null, null, null, null);
        var ana = new User { UserId = "user-ana", UserName = "ana", Email = "ana@example.com" };

        userManager.Setup(value => value.FindByNameAsync(It.IsAny<string>()))
            .ReturnsAsync((string name) => string.Equals(name, "ana", StringComparison.OrdinalIgnoreCase) ? ana : null);
        userManager.Setup(value => value.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((string email) => string.Equals(email, "ana@example.com", StringComparison.OrdinalIgnoreCase) ? ana : null);

        return userManager.Object;
    }

    private static IRoleService CreateRoleService()
    {
        var roleService = new Mock<IRoleService>();

        roleService.Setup(value => value.GetRolesAsync()).ReturnsAsync(
            new[] { "Administrator", "Support", "Anonymous", "Authenticated" }.Select(name => (IRole)new TestRole(name)).ToList());

        return roleService.Object;
    }

    private sealed class TestRole : IRole
    {
        public TestRole(string name)
        {
            RoleName = name;
        }

        public string RoleName { get; }

        public string RoleDescription => null;
    }

    private sealed class FakeProvisioner : IChildTenantDatabaseProvisioner
    {
        public bool CanProvision(ChildDatabaseProvisioningContext context)
            => context.Strategy == ChildDatabaseStrategy.SqlitePerChild;

        public Task<ChildDatabaseProvisioningResult> ProvisionAsync(ChildDatabaseProvisioningContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(new ChildDatabaseProvisioningResult
            {
                DatabaseProvider = "Sqlite",
                ConnectionString = "Data Source=secret-canary.db;Password=hunter2",
                DatabaseName = "secret-canary",
            });

        public Task DeprovisionAsync(ChildDatabaseProvisioningContext context, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}

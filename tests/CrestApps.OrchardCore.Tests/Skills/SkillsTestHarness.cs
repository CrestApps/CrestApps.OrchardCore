using System.Globalization;
using CrestApps.OrchardCore.Skills;
using CrestApps.OrchardCore.Skills.Core;
using CrestApps.OrchardCore.Skills.Core.Indexes;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;
using YesSql.Indexes;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Skills;

/// <summary>
/// A real SQLite store with the Skills services, for tests that need the services end to end.
/// </summary>
internal sealed class SkillsTestHarness : IAsyncDisposable
{
    private readonly string _databasePath;
    private readonly ServiceProvider _provider;

    private SkillsTestHarness(string databasePath, IStore store, ServiceProvider provider)
    {
        _databasePath = databasePath;
        Store = store;
        _provider = provider;
    }

    public IStore Store { get; }

    public static DateTime Now { get; } = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);

    public static DateOnly Today { get; } = new(2026, 11, 2);

    public static async Task<SkillsTestHarness> CreateAsync(
        Action<IServiceCollection> configure = null,
        Func<ISchemaBuilder, Task> createSchema = null,
        IIndexProvider[] indexProviders = null,
        string[] collections = null)
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"skills-{Guid.NewGuid():N}.db");
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));

        store.RegisterIndexes(SkillsIndexProviders.All().Concat(indexProviders ?? []).ToArray());
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(SkillsConstants.CollectionName, TestContext.Current.CancellationToken);

        foreach (var collection in collections ?? [])
        {
            await store.InitializeCollectionAsync(collection, TestContext.Current.CancellationToken);
        }

        await using (var session = store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

            await SkillsIndexSchema.CreateAsync(schemaBuilder);

            if (createSchema is not null)
            {
                await createSchema(schemaBuilder);
            }

            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        var services = new ServiceCollection();

        services.AddSingleton(store);
        services.AddScoped(_ => store.CreateSession());
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughLocalizer<>));
        services.AddSingleton<IDistributedLock, NoOpDistributedLock>();

        var clock = new Mock<IClock>();

        clock.SetupGet(c => c.UtcNow).Returns(Now);
        services.AddSingleton(clock.Object);
        services.AddSkillsCore();

        configure?.Invoke(services);

        return new SkillsTestHarness(databasePath, store, services.BuildServiceProvider());
    }

    public async Task<T> RunAsync<T>(Func<IServiceProvider, Task<T>> work, bool commit = true)
    {
        await using var scope = _provider.CreateAsyncScope();
        var result = await work(scope.ServiceProvider);

        if (commit)
        {
            await scope.ServiceProvider.GetRequiredService<ISession>().SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return result;
    }

    public Task RunAsync(Func<IServiceProvider, Task> work, bool commit = true)
    {
        return RunAsync<object>(async services =>
        {
            await work(services);

            return null;
        }, commit);
    }

    public Task<Skill> AddSkillAsync(string name, Action<Skill> configure = null)
    {
        return RunAsync(async services =>
        {
            var manager = services.GetRequiredService<ISkillManager>();
            var skill = await manager.NewAsync();

            skill.Name = name;
            configure?.Invoke(skill);

            var validation = await manager.ValidateAsync(skill);

            Assert.True(validation.Succeeded, string.Join("; ", validation.Errors.Select(error => error.ErrorMessage)));

            await manager.CreateAsync(skill);

            return skill;
        });
    }

    public Task<Certification> AddCertificationAsync(string name, Action<Certification> configure = null)
    {
        return RunAsync(async services =>
        {
            var manager = services.GetRequiredService<ICertificationManager>();
            var certification = await manager.NewAsync();

            certification.Name = name;
            configure?.Invoke(certification);

            var validation = await manager.ValidateAsync(certification);

            Assert.True(validation.Succeeded, string.Join("; ", validation.Errors.Select(error => error.ErrorMessage)));

            await manager.CreateAsync(certification);

            return certification;
        });
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        TemporarySqliteDatabase.DisposeAndDelete(Store, _databasePath);
    }

    private sealed class PassThroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class NoOpDistributedLock : IDistributedLock
    {
        public Task<ILocker> AcquireLockAsync(string key, TimeSpan? expiration = null)
            => Task.FromResult<ILocker>(new NoOpLocker());

        public Task<(ILocker locker, bool locked)> TryAcquireLockAsync(string key, TimeSpan timeout, TimeSpan? expiration = null)
            => Task.FromResult<(ILocker, bool)>((new NoOpLocker(), true));

        public Task<bool> IsLockAcquiredAsync(string key)
            => Task.FromResult(false);
    }

    private sealed class NoOpLocker : ILocker
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void Dispose()
        {
        }
    }
}

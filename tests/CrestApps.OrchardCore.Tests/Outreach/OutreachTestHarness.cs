using System.Text.Json;
using CrestApps.OrchardCore.Outreach;
using CrestApps.OrchardCore.Outreach.Core.Indexes;
using CrestApps.OrchardCore.Outreach.Core.Models;
using CrestApps.OrchardCore.Outreach.Core.Services;
using CrestApps.OrchardCore.Outreach.Models;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OrchardCore.Modules;
using YesSql;
using YesSql.Indexes;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Outreach;

/// <summary>
/// A real SQLite store with the outreach engine, for tests that send runs end to end.
/// </summary>
internal sealed class OutreachTestHarness : IAsyncDisposable
{
    private readonly string _databasePath;
    private readonly ServiceProvider _provider;

    private OutreachTestHarness(string databasePath, IStore store, ServiceProvider provider, OutreachTestClock clock)
    {
        _databasePath = databasePath;
        Store = store;
        _provider = provider;
        Clock = clock;
    }

    public IStore Store { get; }

    public OutreachTestClock Clock { get; }

    public IServiceProvider Services
        => _provider;

    public static async Task<OutreachTestHarness> CreateAsync(Action<IServiceCollection> configure)
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"outreach-{Guid.NewGuid():N}.db");
        var store = StoreFactory.Create(configuration =>
        {
            configuration.UseSqLite($"Data Source={databasePath};Pooling=False");
            configuration.ContentSerializer = new global::YesSql.Serialization.DefaultContentJsonSerializer(JOptions.Default);
        });

        store.RegisterIndexes(new IIndexProvider[] { new OutreachRunIndexProvider(), new OutreachBatchIndexProvider() });
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(OutreachConstants.CollectionName, TestContext.Current.CancellationToken);

        await using (var session = store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);

            await OutreachIndexSchema.CreateAsync(new SchemaBuilder(store.Configuration, transaction));
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        var clock = new OutreachTestClock();
        var services = new ServiceCollection();

        services.AddSingleton(store);
        services.AddScoped(_ => store.CreateSession());
        services.AddSingleton<IClock>(clock);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddOutreachEngine();
        configure?.Invoke(services);

        return new OutreachTestHarness(databasePath, store, services.BuildServiceProvider(), clock);
    }

    /// <summary>
    /// Starts a run and saves it.
    /// </summary>
    public async Task<OutreachRun> StartAsync(OutreachRun run)
    {
        await using var scope = _provider.CreateAsyncScope();

        var started = await scope.ServiceProvider.GetRequiredService<IOutreachEngine>().StartAsync(run, TestContext.Current.CancellationToken);
        await scope.ServiceProvider.GetRequiredService<ISession>().SaveChangesAsync(TestContext.Current.CancellationToken);

        return started;
    }

    /// <summary>
    /// Runs the background pass once.
    /// </summary>
    public async Task<int> ProcessDueAsync(int maxBatches = 100)
    {
        await using var scope = _provider.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<OutreachBatchProcessor>().ProcessDueAsync(maxBatches, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Processes one batch in its own scope.
    /// </summary>
    public async Task<OutreachBatchOutcome> ProcessBatchAsync(string batchId)
    {
        await using var scope = _provider.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<OutreachBatchProcessor>().ProcessBatchAsync(batchId, TestContext.Current.CancellationToken);
    }

    public async Task<bool> CancelAsync(string runId)
    {
        await using var scope = _provider.CreateAsyncScope();

        var canceled = await scope.ServiceProvider.GetRequiredService<IOutreachEngine>().CancelAsync(runId, TestContext.Current.CancellationToken);
        await scope.ServiceProvider.GetRequiredService<ISession>().SaveChangesAsync(TestContext.Current.CancellationToken);

        return canceled;
    }

    public async Task<OutreachRun> GetRunAsync(string runId)
    {
        await using var session = Store.CreateSession();

        return await session.Query<OutreachRun, OutreachRunIndex>(index => index.ItemId == runId, collection: OutreachConstants.CollectionName)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
    }

    public async Task<IReadOnlyList<OutreachBatch>> GetBatchesAsync(string runId)
    {
        await using var session = Store.CreateSession();

        var batches = await session.Query<OutreachBatch, OutreachBatchIndex>(index => index.RunId == runId, collection: OutreachConstants.CollectionName)
            .ListAsync(TestContext.Current.CancellationToken);

        return batches.OrderBy(batch => batch.Sequence).ToArray();
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        TemporarySqliteDatabase.DisposeAndDelete(Store, _databasePath);
    }
}

using System.Security.Claims;
using CrestApps.OrchardCore.DataConnectors;
using CrestApps.OrchardCore.DataConnectors.DataSources;
using CrestApps.OrchardCore.DataConnectors.Models;
using CrestApps.OrchardCore.DataConnectors.Services;
using CrestApps.OrchardCore.DataConnectors.Storage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrchardCore.DataSources;
using YesSql;

namespace CrestApps.OrchardCore.Tests.DataConnectors;

/// <summary>
/// Refreshes imported data sets end to end: a test connector, the YesSql stores and the row table on SQLite, and the
/// imported data source reading what the refresh published.
/// </summary>
public sealed class DataSetRefreshServiceTests
{
    private static readonly ClaimsPrincipal _reader = PermissionClaimsAuthorizationService.UserWith(DataConnectorPermissions.ReadAllConnectedDataSets.Name);

    [Fact]
    public async Task RefreshAsync_FirstImport_PublishesTheRows_AndTheImportedDataSourceReadsThem()
    {
        // Arrange
        await using var harness = await Harness.CreateAsync();
        harness.Connector.Rows = [DataConnectorTestData.Row(1, "Ada", 1), DataConnectorTestData.Row(2, "Grace", 2)];
        var dataSet = await harness.AddDataSetAsync(DataConnectorTestData.DataSet());

        // Act
        var run = await harness.RefreshAsync(dataSet);
        var rows = await harness.ReadImportedAsync(dataSet.Name, _reader);

        // Assert
        Assert.Equal(DataSetRefreshStatus.Succeeded, run.Status);
        Assert.Equal(2, run.RowCount);
        Assert.Equal(["Ada", "Grace"], rows.Rows.Select(row => row[1]));
        Assert.Equal(new DateTime(2026, 10, 2, 8, 0, 0, DateTimeKind.Utc), rows.Rows[1][2]);
        Assert.Equal(DateTimeKind.Utc, ((DateTime)rows.Rows[1][2]).Kind);

        var saved = await harness.FindDataSetAsync(dataSet.ItemId);
        Assert.Equal(run.Version, saved.Import.ActiveVersion);
        Assert.Equal(2, saved.Import.RowCount);
        Assert.Null(saved.Import.LastError);
    }

    [Fact]
    public async Task RefreshAsync_ThatFailsHalfway_KeepsTheLastPublishedRows_AndDeletesTheStagedOnes()
    {
        // Arrange
        await using var harness = await Harness.CreateAsync();
        harness.Connector.Rows = [DataConnectorTestData.Row(1, "Ada", 1)];
        var dataSet = await harness.AddDataSetAsync(DataConnectorTestData.DataSet());
        var first = await harness.RefreshAsync(dataSet);

        harness.Connector.Rows = [.. Enumerable.Range(1, 5).Select(index => DataConnectorTestData.Row(index, "New " + index, 3))];
        harness.Connector.FailAfterFirstBatch = new DataConnectorException("The API went away.");
        harness.BatchSize = 2;

        // Act
        var failed = await harness.RefreshAsync(await harness.FindDataSetAsync(dataSet.ItemId));
        var rows = await harness.ReadImportedAsync(dataSet.Name, _reader);

        // Assert
        Assert.Equal(DataSetRefreshStatus.Failed, failed.Status);
        Assert.Equal("The API went away.", failed.Error);
        Assert.Equal(["Ada"], rows.Rows.Select(row => row[1]));
        Assert.Equal(0, await harness.RowStore.CountAsync(dataSet.ItemId, failed.Version, TestContext.Current.CancellationToken));

        var saved = await harness.FindDataSetAsync(dataSet.ItemId);
        Assert.Equal(first.Version, saved.Import.ActiveVersion);
        Assert.Equal(DataSetRefreshStatus.Failed, saved.Import.LastStatus);
        Assert.Equal("The API went away.", saved.Import.LastError);
    }

    [Fact]
    public async Task RefreshAsync_Incremental_ReadsOnlyChangedRows_AndReplacesThemByIdentifier()
    {
        // Arrange
        await using var harness = await Harness.CreateAsync();
        harness.Connector.Rows = [DataConnectorTestData.Row(1, "Ada", 1), DataConnectorTestData.Row(2, "Grace", 2)];

        var dataSet = DataConnectorTestData.DataSet();
        dataSet.Refresh = new DataSetRefreshSettings { Mode = DataSetRefreshMode.Incremental, IncrementalField = "Modified" };
        dataSet = await harness.AddDataSetAsync(dataSet);

        var first = await harness.RefreshAsync(dataSet);

        // Grace changed and Linus is new; Ada didn't change.
        harness.Connector.Rows =
        [
            DataConnectorTestData.Row(1, "Ada", 1),
            DataConnectorTestData.Row(2, "Grace Hopper", 5),
            DataConnectorTestData.Row(3, "Linus", 6),
        ];

        // Act
        var second = await harness.RefreshAsync(await harness.FindDataSetAsync(dataSet.ItemId));
        var rows = await harness.ReadImportedAsync(dataSet.Name, _reader);

        // Assert
        Assert.Equal(DataSetRefreshMode.Full, first.Mode);
        Assert.Equal(DataSetRefreshMode.Incremental, second.Mode);
        Assert.Equal(2, second.RowsRead);
        Assert.Equal(3, second.RowCount);
        Assert.Equal(new DateTime(2026, 10, 2, 8, 0, 0, DateTimeKind.Utc), harness.Connector.Reads[^1].Watermark);
        Assert.Equal(["Ada", "Grace Hopper", "Linus"], rows.Rows.Select(row => row[1]).Order());
    }

    [Fact]
    public async Task RefreshAsync_WhenTheSourceGainsAField_AndThePolicyAccepts_AddsItAndReadsEveryRow()
    {
        // Arrange
        await using var harness = await Harness.CreateAsync();
        harness.Connector.Rows = [DataConnectorTestData.Row(1, "Ada", 1)];

        var dataSet = DataConnectorTestData.DataSet();
        dataSet.Refresh = new DataSetRefreshSettings { Mode = DataSetRefreshMode.Incremental, IncrementalField = "Modified" };
        dataSet = await harness.AddDataSetAsync(dataSet);
        await harness.RefreshAsync(dataSet);

        harness.Connector.Fields.Add(new DataField("email", "email", DataFieldType.Text));
        harness.Connector.Rows = [[1L, "Ada", new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc), "ada@example.com"]];

        // Act
        var run = await harness.RefreshAsync(await harness.FindDataSetAsync(dataSet.ItemId));
        var rows = await harness.ReadImportedAsync(dataSet.Name, _reader);

        // Assert
        Assert.Equal(DataSetRefreshStatus.Succeeded, run.Status);
        Assert.Equal(DataSetRefreshMode.Full, run.Mode);
        Assert.Contains("email", run.SchemaChanges, StringComparison.Ordinal);
        Assert.Equal("ada@example.com", rows.Rows[0][rows.Fields.IndexOf("email")]);
    }

    [Fact]
    public async Task RefreshAsync_WhenTheSourceLosesAField_AndThePolicyFails_KeepsThePublishedRows()
    {
        // Arrange
        await using var harness = await Harness.CreateAsync();
        harness.Connector.Rows = [DataConnectorTestData.Row(1, "Ada", 1)];

        var dataSet = DataConnectorTestData.DataSet();
        dataSet.Refresh = new DataSetRefreshSettings { SchemaChangePolicy = DataSetSchemaChangePolicy.Fail };
        dataSet = await harness.AddDataSetAsync(dataSet);
        var first = await harness.RefreshAsync(dataSet);

        harness.Connector.Fields.RemoveAt(1);

        // Act
        var run = await harness.RefreshAsync(await harness.FindDataSetAsync(dataSet.ItemId));

        // Assert
        Assert.Equal(DataSetRefreshStatus.Failed, run.Status);
        Assert.Contains("Removed: name", run.Error, StringComparison.Ordinal);
        Assert.Equal(first.Version, (await harness.FindDataSetAsync(dataSet.ItemId)).Import.ActiveVersion);
    }

    [Fact]
    public async Task RefreshAsync_WhileAnotherRefreshHoldsTheDataSet_IsSkipped_AndReadsNothing()
    {
        // Arrange
        await using var harness = await Harness.CreateAsync();
        var dataSet = await harness.AddDataSetAsync(DataConnectorTestData.DataSet());

        await using var held = (await harness.Lock.TryAcquireLockAsync($"CrestApps.DataConnectors.Refresh:{dataSet.ItemId}", TimeSpan.Zero)).locker;

        // Act
        var run = await harness.RefreshAsync(dataSet);

        // Assert
        Assert.Equal(DataSetRefreshStatus.Skipped, run.Status);
        Assert.Empty(harness.Connector.Reads);
    }

    [Fact]
    public async Task RefreshAsync_ThatRunsPastItsTimeout_FailsAndSaysSo()
    {
        // Arrange
        await using var harness = await Harness.CreateAsync(options => options.RefreshTimeout = TimeSpan.FromMilliseconds(1));
        harness.Connector.Rows = [.. Enumerable.Range(1, 2000).Select(index => DataConnectorTestData.Row(index, "x", 1))];
        harness.BatchSize = 1;
        var dataSet = await harness.AddDataSetAsync(DataConnectorTestData.DataSet());

        // Act
        var run = await harness.RefreshAsync(dataSet);

        // Assert
        Assert.Equal(DataSetRefreshStatus.Failed, run.Status);
        Assert.Contains("took longer", run.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RefreshAsync_KeepsThePreviousVersionForReaders_AndDropsOlderOnes()
    {
        // Arrange
        await using var harness = await Harness.CreateAsync();
        harness.Connector.Rows = [DataConnectorTestData.Row(1, "Ada", 1)];
        var dataSet = await harness.AddDataSetAsync(DataConnectorTestData.DataSet());

        // Act
        var first = await harness.RefreshAsync(dataSet);
        var second = await harness.RefreshAsync(await harness.FindDataSetAsync(dataSet.ItemId));
        var third = await harness.RefreshAsync(await harness.FindDataSetAsync(dataSet.ItemId));

        // Assert
        Assert.Equal(0, await harness.RowStore.CountAsync(dataSet.ItemId, first.Version, TestContext.Current.CancellationToken));
        Assert.Equal(1, await harness.RowStore.CountAsync(dataSet.ItemId, second.Version, TestContext.Current.CancellationToken));
        Assert.Equal(1, await harness.RowStore.CountAsync(dataSet.ItemId, third.Version, TestContext.Current.CancellationToken));

        var saved = await harness.FindDataSetAsync(dataSet.ItemId);
        Assert.Equal(third.Version, saved.Import.ActiveVersion);
        Assert.Equal(second.Version, saved.Import.PreviousVersion);
    }

    [Fact]
    public async Task RefreshDueAsync_RefreshesWhatWasAskedFor_AndWhatIsDue_ButNotLiveOrDisabledDataSets()
    {
        // Arrange
        await using var harness = await Harness.CreateAsync();
        harness.Connector.Rows = [DataConnectorTestData.Row(1, "Ada", 1)];

        var requested = DataConnectorTestData.DataSet(name: "Requested");
        requested.ItemId = "requested";
        requested.Import.RefreshRequestedUtc = harness.Clock.UtcNow;

        var due = DataConnectorTestData.DataSet(name: "Due");
        due.ItemId = "due";
        due.Refresh.IntervalMinutes = 60;
        due.Import.NextRefreshUtc = harness.Clock.UtcNow.AddMinutes(-1);

        var later = DataConnectorTestData.DataSet(name: "Later");
        later.ItemId = "later";
        later.Import.NextRefreshUtc = harness.Clock.UtcNow.AddMinutes(30);

        var live = DataConnectorTestData.DataSet(DataSetAccessMode.Live, "Live");
        live.ItemId = "live";
        live.Import.RefreshRequestedUtc = harness.Clock.UtcNow;

        var disabled = DataConnectorTestData.DataSet(name: "Disabled");
        disabled.ItemId = "disabled";
        disabled.IsEnabled = false;
        disabled.Import.RefreshRequestedUtc = harness.Clock.UtcNow;

        foreach (var dataSet in new[] { requested, due, later, live, disabled })
        {
            await harness.AddDataSetAsync(dataSet);
        }

        // Act
        var count = await harness.RefreshDueAsync();

        // Assert
        Assert.Equal(2, count);
        Assert.NotNull((await harness.FindDataSetAsync("requested")).Import.ActiveVersion);
        Assert.Null((await harness.FindDataSetAsync("requested")).Import.RefreshRequestedUtc);

        var refreshedDue = await harness.FindDataSetAsync("due");
        Assert.NotNull(refreshedDue.Import.ActiveVersion);
        Assert.Equal(harness.Clock.UtcNow.AddMinutes(60), refreshedDue.Import.NextRefreshUtc);
        Assert.Null((await harness.FindDataSetAsync("later")).Import.ActiveVersion);
        Assert.Null((await harness.FindDataSetAsync("live")).Import.ActiveVersion);
        Assert.Null((await harness.FindDataSetAsync("disabled")).Import.ActiveVersion);
    }

    /// <summary>
    /// The refresh service with its real stores on SQLite and a test connector.
    /// </summary>
    internal sealed class Harness : IAsyncDisposable
    {
        private DataConnectorsSqliteStore _fixture;
        private ServiceProvider _services;

        public TestDataConnector Connector { get; } = new();

        public InMemoryDistributedLock Lock { get; } = new();

        public FixedClock Clock { get; } = new();

        public DataConnectorsOptions Options { get; } = new();

        public SqlDataSetRowStore RowStore { get; private set; }

        public int BatchSize
        {
            get => Options.ImportBatchSize;
            set => Options.ImportBatchSize = value;
        }

        public static async Task<Harness> CreateAsync(Action<DataConnectorsOptions> configure = null)
        {
            var harness = new Harness();
            configure?.Invoke(harness.Options);

            harness._fixture = await DataConnectorsSqliteStore.CreateAsync();
            harness.RowStore = harness._fixture.CreateRowStore();

            var services = new ServiceCollection();
            services.AddOptions();
            services.AddLogging();
            services.AddSingleton(harness.Connector);
            services.AddDataConnector<TestDataConnector>();
            services.AddSingleton<IOptions<DataConnectorsOptions>>(Microsoft.Extensions.Options.Options.Create(harness.Options));
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            services.AddSingleton(harness._fixture.Store);
            services.AddScoped(serviceProvider => serviceProvider.GetRequiredService<IStore>().CreateSession());
            services.AddScoped<IDataConnectorManager, DataConnectorManager>();
            services.AddScoped<IDataConnectionSecretManager, DataConnectionSecretManager>();
            services.AddScoped<IDataConnectionStore, DataConnectionStore>();
            services.AddScoped<IConnectedDataSetStore, ConnectedDataSetStore>();
            services.AddScoped<IDataSetRefreshRunStore, DataSetRefreshRunStore>();
            services.AddSingleton<IDataSetRowStore>(harness.RowStore);
            services.AddScoped<ConnectedDataReader>();
            services.AddScoped<ConnectedDataSetAuthorizer>();
            services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationService, PermissionClaimsAuthorizationService>();
            services.AddScoped<ImportedDataSource>();
            services.AddScoped<ConnectedDataSource>();
            services.AddSingleton<global::OrchardCore.Locking.Distributed.IDistributedLock>(harness.Lock);
            services.AddSingleton<global::OrchardCore.Modules.IClock>(harness.Clock);
            services.AddScoped<DataSetRefreshService>();

            harness._services = services.BuildServiceProvider();

            await harness.InScopeAsync(async provider =>
            {
                await provider.GetRequiredService<IDataConnectionStore>().CreateAsync(DataConnectorTestData.Connection());
                await provider.GetRequiredService<ISession>().SaveChangesAsync();
            });

            return harness;
        }

        public async Task<ConnectedDataSet> AddDataSetAsync(ConnectedDataSet dataSet)
        {
            await InScopeAsync(async provider =>
            {
                await provider.GetRequiredService<IConnectedDataSetStore>().CreateAsync(dataSet);
                await provider.GetRequiredService<ISession>().SaveChangesAsync();
            });

            return await FindDataSetAsync(dataSet.ItemId);
        }

        public async Task<ConnectedDataSet> FindDataSetAsync(string id)
        {
            ConnectedDataSet dataSet = null;

            await InScopeAsync(async provider => dataSet = await provider.GetRequiredService<IConnectedDataSetStore>().FindByIdAsync(id));

            return dataSet;
        }

        // Each refresh runs in its own scope, with the data set loaded there, as the background task does.
        public async Task<DataSetRefreshRun> RefreshAsync(ConnectedDataSet dataSet)
        {
            DataSetRefreshRun run = null;

            await InScopeAsync(async provider =>
            {
                var current = await provider.GetRequiredService<IConnectedDataSetStore>().FindByIdAsync(dataSet.ItemId) ?? dataSet;

                run = await provider.GetRequiredService<DataSetRefreshService>().RefreshAsync(current, DataSetRefreshTrigger.Manual, "tester", TestContext.Current.CancellationToken);
                await provider.GetRequiredService<ISession>().SaveChangesAsync();
            });

            return run;
        }

        public async Task<int> RefreshDueAsync()
        {
            var count = 0;

            await InScopeAsync(async provider =>
            {
                count = await provider.GetRequiredService<DataSetRefreshService>().RefreshDueAsync(TestContext.Current.CancellationToken);
                await provider.GetRequiredService<ISession>().SaveChangesAsync();
            });

            return count;
        }

        public async Task<DataRowSet> ReadImportedAsync(string name, ClaimsPrincipal user)
        {
            DataRowSet rows = null;

            await InScopeAsync(async provider =>
            {
                var query = new DataSourceQuery { DataSet = name, Context = new DataSourceContext { User = user } };

                rows = await provider.GetRequiredService<ImportedDataSource>().ReadRowsAsync(query, 1_000, TestContext.Current.CancellationToken);
            });

            return rows;
        }

        public async Task InScopeAsync(Func<IServiceProvider, Task> action)
        {
            await using var scope = _services.CreateAsyncScope();

            await action(scope.ServiceProvider);
        }

        public async ValueTask DisposeAsync()
        {
            if (_services is not null)
            {
                await _services.DisposeAsync();
            }

            if (_fixture is not null)
            {
                await _fixture.DisposeAsync();
            }
        }
    }
}

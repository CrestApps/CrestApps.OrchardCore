using CrestApps.OrchardCore.Core.SourceAccess;
using CrestApps.OrchardCore.Disbursements;
using CrestApps.OrchardCore.Disbursements.Core.Indexes;
using CrestApps.OrchardCore.Disbursements.Core.Services;
using CrestApps.OrchardCore.Disbursements.Indexes;
using CrestApps.OrchardCore.Disbursements.Migrations;
using CrestApps.OrchardCore.Disbursements.Models;
using CrestApps.OrchardCore.Disbursements.Reports;
using CrestApps.OrchardCore.Disbursements.Services;
using CrestApps.OrchardCore.SourceAccess;
using CrestApps.OrchardCore.Tests.Checks;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer;
using CrestApps.OrchardCore.Tests.Utilities;
using OrchardCore.DataSources;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Disbursements;

/// <summary>
/// Disbursements on a real SQLite store: the access condition runs in the database for the list and the data set,
/// mixed whole and fractional amounts read back, and batches to settle are found by date.
/// </summary>
public sealed class DisbursementStoreTests : IAsyncLifetime
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"disbursements-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_databasePath};Pooling=False"));
        _store.RegisterIndexes([new DisbursementIndexProvider(), new DisbursementBatchIndexProvider()], DisbursementsConstants.CollectionName);

        await _store.InitializeAsync(cancellationToken);
        await _store.InitializeCollectionAsync(DisbursementsConstants.CollectionName, cancellationToken);

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(cancellationToken);

            Assert.Equal(2, await new DisbursementsMigrations { SchemaBuilder = new SchemaBuilder(_store.Configuration, transaction) }.CreateAsync());

            await transaction.CommitAsync(cancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            // A whole amount and a fractional one: SQLite stores them as INTEGER and REAL, which the index never holds.
            await session.SaveAsync(Disbursement("manual", null, null, 6200m), false, DisbursementsConstants.CollectionName, cancellationToken);
            await session.SaveAsync(Disbursement("groupA", "PayStatement", "groupA", 1234.56m), false, DisbursementsConstants.CollectionName, cancellationToken);
            await session.SaveAsync(Disbursement("groupB", "PayStatement", "groupB", 10m), false, DisbursementsConstants.CollectionName, cancellationToken);
            await session.SaveAsync(new DisbursementBatch { ItemId = "old", Provider = "Nacha", Status = DisbursementBatchStatus.Released, ReleasedUtc = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), CreatedUtc = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc) }, false, DisbursementsConstants.CollectionName, cancellationToken);
            await session.SaveAsync(new DisbursementBatch { ItemId = "new", Provider = "Nacha", Status = DisbursementBatchStatus.Released, ReleasedUtc = new DateTime(2026, 11, 12, 0, 0, 0, DateTimeKind.Utc), CreatedUtc = new DateTime(2026, 11, 12, 0, 0, 0, DateTimeKind.Utc) }, false, DisbursementsConstants.CollectionName, cancellationToken);
            await session.SaveAsync(new DisbursementBatch { ItemId = "batchA", Provider = "Nacha", Status = DisbursementBatchStatus.Open, SourceKind = "PayStatement", AccessScope = "groupA", CreatedUtc = new DateTime(2026, 11, 13, 0, 0, 0, DateTimeKind.Utc) }, false, DisbursementsConstants.CollectionName, cancellationToken);
            await session.SaveAsync(new DisbursementBatch { ItemId = "batchB", Provider = "Nacha", Status = DisbursementBatchStatus.Open, SourceKind = "PayStatement", AccessScope = "groupB", CreatedUtc = new DateTime(2026, 11, 13, 0, 0, 0, DateTimeKind.Utc) }, false, DisbursementsConstants.CollectionName, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

        return ValueTask.CompletedTask;
    }

    [Theory]
    [InlineData("pat", new[] { "groupA", "manual" })]
    [InlineData("lee", new[] { "manual" })]
    public async Task ListPageAsync_ReadsOnlyTheDisbursementsThePersonMaySee(string userName, string[] expected)
    {
        // Arrange
        await using var session = _store.CreateSession();
        var filter = await Access().GetFilterAsync(TestSecurity.User(userName), SourceAccessLevel.View, TestContext.Current.CancellationToken);

        // Act
        var (count, page) = await new DisbursementStore(session).ListPageAsync(new DisbursementQuery { Access = filter }, 0, 50, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expected.Length, count);
        Assert.Equal(expected, page.Select(entry => entry.ItemId).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task DataSet_ReadsOnlyTheDisbursementsThePersonMaySee_WithTheirAmounts()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var dataSet = new DisbursementsReportDataSet(session, TestSecurity.Granting(DisbursementsPermissions.ViewDisbursements), Access(), RecipeRoundTrip.Localizer<DisbursementsReportDataSet>());
        var query = new DataSourceQuery
        {
            DataSet = DisbursementsReportDataSet.DataSetName,
            Fields = new HashSet<string>(["ItemId", "Amount"], StringComparer.Ordinal),
            MaxRows = 100,
            Context = new DataSourceContext { User = TestSecurity.User("pat") },
        };

        // Act
        var table = await ReportDesignerTestServices.ReadAsync(new DisbursementsReportDataSource([dataSet]), query, TestContext.Current.CancellationToken);

        // Assert
        var ids = table.Fields.ToList().FindIndex(field => field.Name == "ItemId");
        var amounts = table.Fields.ToList().FindIndex(field => field.Name == "Amount");
        var rows = table.Rows.ToDictionary(row => (string)row[ids], row => Convert.ToDecimal(row[amounts], System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(2, rows.Count);
        Assert.Equal(6200m, rows["manual"]);
        Assert.Equal(1234.56m, rows["groupA"]);
    }

    [Fact]
    public async Task ListToSettleAsync_FindsReleasedBatchesOlderThanTheDate()
    {
        // Arrange
        await using var session = _store.CreateSession();

        // Act
        var batches = await new DisbursementBatchStore(session).ListToSettleAsync(new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc), 10, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("old", Assert.Single(batches).ItemId);
    }

    [Theory]
    [InlineData("pat", new[] { "batchA", "new", "old" })]
    [InlineData("lee", new[] { "new", "old" })]
    public async Task BatchListPageAsync_CountsAndReadsOnlyTheBatchesThePersonMayActOn(string userName, string[] expected)
    {
        // Arrange
        await using var session = _store.CreateSession();
        var filter = await Access().GetFilterAsync(TestSecurity.User(userName), SourceAccessLevel.Manage, TestContext.Current.CancellationToken);

        // Act
        var (count, page) = await new DisbursementBatchStore(session).ListPageAsync(null, filter, 0, 50, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expected.Length, count);
        Assert.Equal(expected, page.Select(entry => entry.ItemId).Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReleaseBatchAsync_AfterItsCommit_SavesTheBatchAndTheDisbursementsOnce(bool providerSucceeds)
    {
        // Arrange: a batch of one disbursement, released through a service whose commit saves the session, as on a site.
        var cancellationToken = TestContext.Current.CancellationToken;

        await using (var setup = _store.CreateSession())
        {
            var batched = Disbursement("release", "PayStatement", "groupA", 500m);

            batched.Status = DisbursementStatus.Batched;
            batched.BatchId = "releaseBatch";
            batched.Method = "Fake";
            await setup.SaveAsync(batched, false, DisbursementsConstants.CollectionName, cancellationToken);
            await setup.SaveAsync(new DisbursementBatch { ItemId = "releaseBatch", Provider = "Fake", Status = DisbursementBatchStatus.Open, DisbursementIds = ["release"], CreatedUtc = new DateTime(2026, 11, 13, 0, 0, 0, DateTimeKind.Utc) }, false, DisbursementsConstants.CollectionName, cancellationToken);
            await setup.SaveChangesAsync(cancellationToken);
        }

        var context = new DisbursementsTestContext();

        await using (var session = _store.CreateSession())
        {
            var clock = new Moq.Mock<global::OrchardCore.Modules.IClock>();

            clock.SetupGet(entry => entry.UtcNow).Returns(new DateTime(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc));

            var manager = new DisbursementManager(new DisbursementStore(session), [new CrestApps.OrchardCore.Disbursements.Core.Handlers.DisbursementHandler(clock.Object)], Microsoft.Extensions.Logging.Abstractions.NullLogger<CrestApps.Core.Services.CatalogManager<Disbursement>>.Instance);
            var batchStore = new DisbursementBatchStore(session);
            var batches = new DisbursementBatchManager(batchStore, [new CrestApps.OrchardCore.Disbursements.Core.Handlers.DisbursementBatchHandler(clock.Object)], Microsoft.Extensions.Logging.Abstractions.NullLogger<CrestApps.Core.Services.CatalogManager<DisbursementBatch>>.Instance);
            var service = new DisbursementService(
                manager,
                batches,
                batchStore,
                context.Files,
                context.Banking.Manager,
                context.Banking.Service,
                [new FakeProvider(providerSucceeds)],
                [],
                context.Banking.HttpContextAccessor,
                context.Locks,
                new SessionCommitter(session),
                context.Checks.LocalClock,
                clock.Object,
                RecipeRoundTrip.Localizer<DisbursementService>());

            // Act
            var errors = await service.ReleaseBatchAsync(await batches.FindByIdAsync("releaseBatch", cancellationToken), cancellationToken);

            await session.SaveChangesAsync(cancellationToken);

            Assert.Equal(providerSucceeds, errors.Count == 0);
        }

        // Assert: one row each, in the state the release left.
        await using var read = _store.CreateSession();
        var batchRows = await read.Query<DisbursementBatch, DisbursementBatchIndex>(index => index.ItemId == "releaseBatch", DisbursementsConstants.CollectionName).ListAsync(cancellationToken);
        var disbursementRows = await read.Query<Disbursement, DisbursementIndex>(index => index.ItemId == "release", DisbursementsConstants.CollectionName).ListAsync(cancellationToken);

        Assert.Equal(providerSucceeds ? DisbursementBatchStatus.Released : DisbursementBatchStatus.Open, Assert.Single(batchRows).Status);
        Assert.Equal(providerSucceeds ? DisbursementStatus.Submitted : DisbursementStatus.Batched, Assert.Single(disbursementRows).Status);
    }

    /// <summary>
    /// Saves the session, as the site's committer does.
    /// </summary>
    private sealed class SessionCommitter : IDisbursementCommitter
    {
        private readonly ISession _session;

        public SessionCommitter(ISession session)
        {
            _session = session;
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
            => _session.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Sends a batch, or says nothing was sent.
    /// </summary>
    private sealed class FakeProvider : IDisbursementProvider
    {
        private readonly bool _succeeds;

        public FakeProvider(bool succeeds)
        {
            _succeeds = succeeds;
        }

        public string Name => "Fake";

        public Microsoft.Extensions.Localization.LocalizedString DisplayName => new("Fake", "Fake");

        public string Method => "Fake";

        public Task<DisbursementSubmitResult> SubmitAsync(DisbursementBatch batch, IReadOnlyList<Disbursement> disbursements, CancellationToken cancellationToken = default)
            => Task.FromResult(_succeeds ? new DisbursementSubmitResult { Succeeded = true } : new DisbursementSubmitResult { Errors = ["The bank refused the file."] });
    }

    private static SourceAccessService Access()
        => new([new FixedSourceAccess("PayStatement", new Dictionary<string, SourceAccessScope> { ["pat"] = SourceAccessScope.Of(["groupA"]) })]);

    private static Disbursement Disbursement(string id, string sourceKind, string scope, decimal amount)
        => new()
        {
            ItemId = id,
            PayeeName = "Payee " + id,
            Amount = amount,
            Currency = "USD",
            Method = "Manual",
            SourceKind = sourceKind,
            SourceId = sourceKind is null ? null : "source-" + id,
            AccessScope = scope,
            IdempotencyKey = "key-" + id,
            Status = DisbursementStatus.Pending,
            CreatedUtc = new DateTime(2026, 11, 13, 9, 0, 0, DateTimeKind.Utc),
        };
}

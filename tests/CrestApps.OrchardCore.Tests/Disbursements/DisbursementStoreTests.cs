using CrestApps.OrchardCore.Core.SourceAccess;
using CrestApps.OrchardCore.Disbursements;
using CrestApps.OrchardCore.Disbursements.Core.Services;
using CrestApps.OrchardCore.Disbursements.Indexes;
using CrestApps.OrchardCore.Disbursements.Migrations;
using CrestApps.OrchardCore.Disbursements.Models;
using CrestApps.OrchardCore.Disbursements.Reports;
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

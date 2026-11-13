using CrestApps.OrchardCore.Checks;
using CrestApps.OrchardCore.Checks.Core.Indexes;
using CrestApps.OrchardCore.Checks.Core.Services;
using CrestApps.OrchardCore.Core.SourceAccess;
using CrestApps.OrchardCore.Checks.Indexes;
using CrestApps.OrchardCore.Checks.Migrations;
using CrestApps.OrchardCore.Checks.Models;
using CrestApps.OrchardCore.Checks.Reports;
using CrestApps.OrchardCore.SourceAccess;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer;
using CrestApps.OrchardCore.Tests.Utilities;
using Moq;
using OrchardCore.DataSources;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Checks;

/// <summary>
/// The check register on a real SQLite store: the access condition runs in the database for the register page and the
/// Check Register data set, stale checks are found by date, and the migration indexes when each check last changed.
/// </summary>
public sealed class CheckRegisterStoreTests : IAsyncLifetime
{
    private const string Paychecks = "Paychecks";

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"check-register-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_databasePath};Pooling=False"));
        _store.RegisterIndexes([new CheckRecordIndexProvider()], ChecksConstants.CollectionName);

        await _store.InitializeAsync(cancellationToken);
        await _store.InitializeCollectionAsync(ChecksConstants.CollectionName, cancellationToken);

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(cancellationToken);
            var migrations = new ChecksMigrations(_store) { SchemaBuilder = new SchemaBuilder(_store.Configuration, transaction) };

            Assert.Equal(1, await migrations.CreateAsync());
            Assert.Equal(2, await migrations.UpdateFrom1Async());

            await transaction.CommitAsync(cancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            await session.SaveAsync(Check("manual", 1001, null, null, new DateOnly(2026, 3, 1), CheckStatus.Printed), false, ChecksConstants.CollectionName, cancellationToken);
            await session.SaveAsync(Check("groupA", 1002, Paychecks, "groupA", new DateOnly(2026, 11, 1), CheckStatus.Printed), false, ChecksConstants.CollectionName, cancellationToken);
            await session.SaveAsync(Check("groupB", 1003, Paychecks, "groupB", new DateOnly(2026, 3, 2), CheckStatus.Issued), false, ChecksConstants.CollectionName, cancellationToken);
            await session.SaveAsync(Check("other", 1004, "Refunds", null, new DateOnly(2026, 11, 2), CheckStatus.Issued), false, ChecksConstants.CollectionName, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

        return ValueTask.CompletedTask;
    }

    [Theory]
    [InlineData("pat", new[] { "manual", "groupA", "other" })]
    [InlineData("lee", new[] { "manual", "other" })]
    [InlineData("ann", new[] { "manual", "groupA", "groupB", "other" })]
    public async Task ListPageAsync_ReadsOnlyTheChecksThePersonMaySee(string userName, string[] expected)
    {
        // Arrange
        await using var session = _store.CreateSession();
        var store = new CheckRecordStore(session);
        var filter = await Access().GetFilterAsync(TestSecurity.User(userName), SourceAccessLevel.View, TestContext.Current.CancellationToken);

        // Act
        var (count, checks) = await store.ListPageAsync(new CheckRegisterQuery { Access = filter, OldestFirst = true }, 0, 50, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expected.Length, count);
        Assert.Equal(expected.Order(), checks.Select(check => check.ItemId).Order());
    }

    [Fact]
    public async Task DataSet_ReadsOnlyTheChecksThePersonMaySee()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var dataSet = new CheckRegisterReportDataSet(session, TestSecurity.Granting(ChecksPermissions.ViewCheckRegister), Access(), RecipeRoundTrip.Localizer<CheckRegisterReportDataSet>());
        var source = new ChecksReportDataSource([dataSet]);

        // Act
        var lee = await ReportDesignerTestServices.ReadAsync(source, Query("lee"), TestContext.Current.CancellationToken);
        var pat = await ReportDesignerTestServices.ReadAsync(source, Query("pat"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["manual", "other"], Ids(lee));
        Assert.Equal(["groupA", "manual", "other"], Ids(pat));
    }

    [Fact]
    public async Task DataSet_ReadOnlyNewRows_ReadsOnlyTheChecksChangedSinceTheLastRun()
    {
        // Arrange: what Read only new rows passes on its second run, a condition on the Changed field.
        await using var session = _store.CreateSession();
        var dataSet = new CheckRegisterReportDataSet(session, TestSecurity.Granting(ChecksPermissions.ViewCheckRegister), Access(), RecipeRoundTrip.Localizer<CheckRegisterReportDataSet>());
        var source = new ChecksReportDataSource([dataSet]);
        var query = Query("ann");
        query.Conditions.Add(new DataCondition { Field = "ModifiedUtc", Operator = DataFilterOperator.GreaterThan, Values = [new DateTime(2026, 11, 13, 9, 30, 0, DateTimeKind.Utc)] });

        // Act
        var changed = await ReportDesignerTestServices.ReadAsync(source, query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["groupA"], Ids(changed));
    }

    [Fact]
    public async Task ListPrintedBeforeAsync_FindsOnlyPrintedChecksDatedBefore()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var store = new CheckRecordStore(session);

        // Act
        var checks = await store.ListPrintedBeforeAsync(new DateOnly(2026, 5, 1), 50, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("manual", Assert.Single(checks).ItemId);
    }

    [Fact]
    public async Task TheIndex_HoldsWhenEachCheckLastChanged_AndItsAccessKey()
    {
        // Arrange
        await using var session = _store.CreateSession();

        // Act
        var rows = await session.QueryIndex<CheckRecordIndex>(collection: ChecksConstants.CollectionName).ListAsync(TestContext.Current.CancellationToken);

        // Assert
        var groupA = Assert.Single(rows, row => row.ItemId == "groupA");
        Assert.Equal("Paychecks/groupA", groupA.AccessKey);
        Assert.Equal(new DateTime(2026, 11, 13, 10, 0, 0, DateTimeKind.Utc), groupA.ModifiedUtc);
        Assert.Null(Assert.Single(rows, row => row.ItemId == "manual").AccessKey);
    }

    private static SourceAccessService Access()
        => new([new FixedSourceAccess(Paychecks, new Dictionary<string, SourceAccessScope>
        {
            ["pat"] = SourceAccessScope.Of(["groupA"]),
            ["ann"] = SourceAccessScope.All,
        })]);

    private static CheckRecord Check(string id, long number, string sourceKind, string scope, DateOnly checkDate, CheckStatus status)
        => new()
        {
            ItemId = id,
            BankAccountId = "account1",
            BankAccountName = "Operating",
            Number = number,
            PayeeName = "Payee " + id,
            Amount = 10,
            Currency = "USD",
            CheckDate = checkDate,
            SourceKind = sourceKind,
            SourceId = sourceKind is null ? null : "source-" + id,
            AccessScope = scope,
            Status = status,
            IssuedUtc = new DateTime(2026, 11, 13, 9, 0, 0, DateTimeKind.Utc).AddMinutes(number - 1000),
            ModifiedUtc = id == "groupA" ? new DateTime(2026, 11, 13, 10, 0, 0, DateTimeKind.Utc) : null,
        };

    private static DataSourceQuery Query(string userName)
        => new()
        {
            DataSet = CheckRegisterReportDataSet.DataSetName,
            Fields = new HashSet<string>(["ItemId"], StringComparer.Ordinal),
            MaxRows = 100,
            Context = new DataSourceContext { User = TestSecurity.User(userName) },
        };

    private static string[] Ids(DataRowSet table)
    {
        var index = table.Fields.ToList().FindIndex(field => field.Name == "ItemId");

        return table.Rows.Select(row => (string)row[index]).Order(StringComparer.Ordinal).ToArray();
    }
}

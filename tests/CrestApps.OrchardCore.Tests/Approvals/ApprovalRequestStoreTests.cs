using CrestApps.OrchardCore.Approvals;
using CrestApps.OrchardCore.Approvals.Core.Services;
using CrestApps.OrchardCore.Approvals.Indexes;
using CrestApps.OrchardCore.Approvals.Migrations;
using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.Approvals.Reports;
using CrestApps.OrchardCore.Core.SourceAccess;
using CrestApps.OrchardCore.SourceAccess;
using CrestApps.OrchardCore.Tests.Checks;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer;
using CrestApps.OrchardCore.Tests.Utilities;
using OrchardCore.DataSources;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Approvals;

/// <summary>
/// Approval requests on a real SQLite store: the access condition runs in the database for the list and the data set,
/// and a pending request is found by its subject.
/// </summary>
public sealed class ApprovalRequestStoreTests : IAsyncLifetime
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"approvals-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_databasePath};Pooling=False"));
        _store.RegisterIndexes([new ApprovalRequestIndexProvider()], ApprovalsConstants.CollectionName);

        await _store.InitializeAsync(cancellationToken);
        await _store.InitializeCollectionAsync(ApprovalsConstants.CollectionName, cancellationToken);

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(cancellationToken);

            Assert.Equal(1, await new ApprovalsMigrations { SchemaBuilder = new SchemaBuilder(_store.Configuration, transaction) }.CreateAsync());

            await transaction.CommitAsync(cancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            await session.SaveAsync(Request("expense", "Expense", null, ApprovalStatus.Pending, 120.5m), false, ApprovalsConstants.CollectionName, cancellationToken);
            await session.SaveAsync(Request("groupA", "PayRun", "groupA", ApprovalStatus.Pending, 6200m), false, ApprovalsConstants.CollectionName, cancellationToken);
            await session.SaveAsync(Request("groupB", "PayRun", "groupB", ApprovalStatus.Approved, 10m), false, ApprovalsConstants.CollectionName, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

        return ValueTask.CompletedTask;
    }

    [Theory]
    [InlineData("pat", new[] { "expense", "groupA" })]
    [InlineData("lee", new[] { "expense" })]
    public async Task ListPageAsync_ReadsOnlyTheRequestsThePersonMaySee(string userName, string[] expected)
    {
        // Arrange
        await using var session = _store.CreateSession();
        var filter = await Access().GetFilterAsync(TestSecurity.User(userName), SourceAccessLevel.View, TestContext.Current.CancellationToken);

        // Act
        var (count, page) = await new ApprovalRequestStore(session).ListPageAsync(new ApprovalRequestQuery { Access = filter }, 0, 50, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expected.Length, count);
        Assert.Equal(expected, page.Select(entry => entry.ItemId).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task FindPendingAsync_FindsOnlyAPendingRequestAboutTheSubject()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var store = new ApprovalRequestStore(session);

        // Act
        var pending = await store.FindPendingAsync("PayRun", "run-groupA", null, TestContext.Current.CancellationToken);
        var decided = await store.FindPendingAsync("PayRun", "run-groupB", null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("groupA", pending?.ItemId);
        Assert.Null(decided);
    }

    [Fact]
    public async Task DataSet_ReadsOnlyTheRequestsThePersonMaySee_WithTheirAmounts()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var dataSet = new ApprovalRequestsReportDataSet(session, TestSecurity.Granting(ApprovalsPermissions.ViewApprovalRequests), Access(), RecipeRoundTrip.Localizer<ApprovalRequestsReportDataSet>());
        var query = new DataSourceQuery
        {
            DataSet = ApprovalRequestsReportDataSet.DataSetName,
            Fields = new HashSet<string>(["ItemId", "Amount", "Status"], StringComparer.Ordinal),
            MaxRows = 100,
            Context = new DataSourceContext { User = TestSecurity.User("lee") },
        };

        // Act
        var table = await ReportDesignerTestServices.ReadAsync(new ApprovalsReportDataSource([dataSet]), query, TestContext.Current.CancellationToken);

        // Assert
        var ids = table.Fields.ToList().FindIndex(field => field.Name == "ItemId");
        var amounts = table.Fields.ToList().FindIndex(field => field.Name == "Amount");
        var row = Assert.Single(table.Rows);
        Assert.Equal("expense", row[ids]);
        Assert.Equal(120.5m, Convert.ToDecimal(row[amounts], System.Globalization.CultureInfo.InvariantCulture));
    }

    private static SourceAccessService Access()
        => new([new FixedSourceAccess("PayRun", new Dictionary<string, SourceAccessScope> { ["pat"] = SourceAccessScope.Of(["groupA"]) })]);

    private static ApprovalRequest Request(string id, string subjectKind, string scope, ApprovalStatus status, decimal amount)
        => new()
        {
            ItemId = id,
            SubjectKind = subjectKind,
            SubjectId = "run-" + id,
            AccessScope = scope,
            Title = "Request " + id,
            Amount = amount,
            Currency = "USD",
            PolicyId = "policy",
            Status = status,
            RequesterUserId = "riley-id",
            CreatedUtc = new DateTime(2026, 11, 13, 9, 0, 0, DateTimeKind.Utc),
            ModifiedUtc = new DateTime(2026, 11, 13, 9, 0, 0, DateTimeKind.Utc),
        };
}

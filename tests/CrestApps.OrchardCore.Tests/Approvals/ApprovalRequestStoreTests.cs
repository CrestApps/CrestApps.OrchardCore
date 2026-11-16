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
        var dataSet = new ApprovalRequestsReportDataSet(session, TestSecurity.Granting(ApprovalsPermissions.ViewApprovalRequests), Access(), new ListApprovalTypeCatalog([]), new CrestApps.OrchardCore.Tests.Modules.Reports.Designer.PassThroughStringLocalizerFactory(), RecipeRoundTrip.Localizer<ApprovalRequestsReportDataSet>());
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

    [Fact]
    public async Task TwoApproversDecidingAtOnce_FromTheirOwnSessions_NeverOverwriteEachOther()
    {
        // Arrange: a pending request both Pat and Kim may decide; each loads it in their own unit of work first.
        var cancellationToken = TestContext.Current.CancellationToken;

        await using (var seed = _store.CreateSession())
        {
            var race = Request("race", "PayRun", null, ApprovalStatus.Pending, 50m);

            race.Stages = [new ApprovalStage { Approvers = [new ApproverRule { Kind = ApproverRuleKinds.Permission, Value = "Finance" }], RequiredApprovals = 1 }];
            await seed.SaveAsync(race, false, ApprovalsConstants.CollectionName, cancellationToken);
            await seed.SaveChangesAsync(cancellationToken);
        }

        var closed = new RecordingSubjectHandler();
        await using var sessionA = _store.CreateSession();
        await using var sessionB = _store.CreateSession();
        var (serviceA, managerA) = Service(sessionA, closed);
        var (serviceB, managerB) = Service(sessionB, closed);
        var loadedByPat = await managerA.FindByIdAsync("race", cancellationToken);
        var loadedByKim = await managerB.FindByIdAsync("race", cancellationToken);

        // Act: Pat approves and commits; Kim, who waited, rejects the copy loaded before.
        var approved = await serviceA.DecideAsync(loadedByPat, TestSecurity.User("pat"), ApprovalDecisionKind.Approve, null, cancellationToken);
        var rejected = await serviceB.DecideAsync(loadedByKim, TestSecurity.User("kim"), ApprovalDecisionKind.Reject, "Too late", cancellationToken);

        // Assert: Pat's decision stands, Kim is told why, and the close hook ran once.
        await using var reader = _store.CreateSession();
        var stored = await new ApprovalRequestStore(reader).FindByIdAsync("race", cancellationToken);

        Assert.True(approved.Succeeded, string.Join(" ", approved.Errors));
        Assert.False(rejected.Succeeded);
        Assert.Equal(ApprovalStatus.Approved, stored.Status);
        Assert.Equal(["pat-id"], stored.Decisions.Select(decision => decision.UserId));
        Assert.Equal([ApprovalStatus.Approved], closed.Closed);
    }

    // A service over one session, committing it as the module does.
    private static (ApprovalService Service, ApprovalRequestManager Manager) Service(ISession session, RecordingSubjectHandler handler)
    {
        var clock = new Moq.Mock<global::OrchardCore.Modules.IClock>();

        clock.SetupGet(entry => entry.UtcNow).Returns(new DateTime(2026, 11, 16, 10, 0, 0, DateTimeKind.Utc));

        var manager = new ApprovalRequestManager(new ApprovalRequestStore(session), [new global::CrestApps.OrchardCore.Approvals.Core.Handlers.ApprovalRequestHandler(clock.Object)], Microsoft.Extensions.Logging.Abstractions.NullLogger<global::CrestApps.Core.Services.CatalogManager<ApprovalRequest>>.Instance);
        var policies = new Moq.Mock<global::CrestApps.Core.Services.INamedCatalogManager<ApprovalPolicy>>();
        var service = new ApprovalService(
            manager,
            policies.Object,
            new NamedApproverDirectory(),
            new ApprovalPolicyResolver(policies.Object),
            new ListApprovalTypeCatalog([handler]),
            new SourceAccessService([]),
            [],
            new KeyedDistributedLock(),
            new SessionCommitter(session),
            clock.Object,
            RecipeRoundTrip.Localizer<ApprovalService>());

        return (service, manager);
    }

    private sealed class SessionCommitter : IApprovalCommitter
    {
        private readonly ISession _session;

        public SessionCommitter(ISession session)
        {
            _session = session;
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
            => _session.SaveChangesAsync(cancellationToken);
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

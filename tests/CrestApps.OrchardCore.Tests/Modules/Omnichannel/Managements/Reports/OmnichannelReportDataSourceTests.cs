using System.Security.Claims;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Indexes;
using CrestApps.OrchardCore.Omnichannel.Managements.Reports.DataSources;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Tests.Modules.Reports.Contents;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.Security;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Reports;

/// <summary>
/// The Omnichannel report data source reads activities from a real SQLite store, so the index query, the date
/// push-down, and the permission it requires are exercised end to end.
/// </summary>
public sealed class OmnichannelReportDataSourceTests : IAsyncLifetime
{
    private static readonly ClaimsPrincipal _user = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "u1"), new Claim(ClaimTypes.Name, "analyst")], "Test"));
    private static readonly DateTime _day = new(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"omnichannel-reports-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_databasePath};Pooling=False"));
        _store.RegisterIndexes([new OmnichannelActivityIndexProvider()]);

        await _store.InitializeAsync(TestContext.Current.CancellationToken);
        await _store.InitializeCollectionAsync(OmnichannelConstants.CollectionName, TestContext.Current.CancellationToken);

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);

            // The index table is built from the index's own properties so the test follows the index.
            await new SchemaBuilder(_store.Configuration, transaction).CreateMapIndexTableAsync<OmnichannelActivityIndex>(table =>
            {
                foreach (var property in typeof(OmnichannelActivityIndex).GetProperties().Where(property => property.CanWrite && property.Name is not "Id" and not "DocumentId"))
                {
                    var underlying = Nullable.GetUnderlyingType(property.PropertyType);

                    table.Column(property.Name, underlying ?? property.PropertyType, column => column.Nullable());
                }
            }, collection: OmnichannelConstants.CollectionName);

            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            for (var day = 0; day < 5; day++)
            {
                await session.SaveAsync(new OmnichannelActivity
                {
                    ItemId = "a" + day,
                    Kind = ActivityKind.Call,
                    Channel = OmnichannelConstants.Channels.Phone,
                    Status = day == 0 ? ActivityStatus.Completed : ActivityStatus.NotStated,
                    AssignedToId = "agent-1",
                    DispositionId = day == 0 ? "d-sale" : null,
                    CampaignId = "c1",
                    Attempts = day + 1,
                    CreatedUtc = _day.AddDays(day),
                    ScheduledUtc = _day.AddDays(day),
                    CompletedUtc = day == 0 ? _day.AddMinutes(90) : null,
                }, false, OmnichannelConstants.CollectionName, TestContext.Current.CancellationToken);
            }

            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    public ValueTask DisposeAsync()
    {
        // Deleting retries while another test's connection still holds the file under a parallel run.
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task WithoutViewReports_NothingIsExposed()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session, canViewReports: false);
        var context = new ReportDataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync("Activities", context, TestContext.Current.CancellationToken);
        var table = await source.QueryAsync(Query("Activities"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(schema);
        Assert.Empty(table.Rows);
    }

    [Fact]
    public async Task DataSets_AreListed_WithTheirRelationships()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var context = new ReportDataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync("Activities", context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["Activities", "Dispositions", "Campaigns", "CampaignGroups", "ActivityBatches"], dataSets.Select(dataSet => dataSet.Name));
        Assert.Equal("Dispositions", Assert.Single(schema.FindField("DispositionId").References).DataSet);
        Assert.Equal(ReportsConstants.UsersDataSet, Assert.Single(schema.FindField("AssignedToId").References).DataSet);
        Assert.Equal("DialerProfiles", Assert.Single(schema.FindField("DialerProfileId").References).DataSet);
        Assert.Contains(dataSets[0].References, reference => reference.DataSet == "Campaigns");
    }

    [Fact]
    public async Task Activities_AreReadNewestFirst_WithTypedValuesAndNames()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);

        // Act
        var table = await source.QueryAsync(Query("Activities"), TestContext.Current.CancellationToken);

        // Assert
        var rows = Rows(table);
        Assert.Equal(["a4", "a3", "a2", "a1", "a0"], rows.Select(row => row["ItemId"]));
        var completed = rows[^1];
        Assert.Equal("Call", completed["Kind"]);
        Assert.Equal("Completed", completed["Status"]);
        Assert.Equal(1L, completed["Attempts"]);
        Assert.Equal("Sale", completed["Disposition"]);
        Assert.Equal("Spring drive", completed["Campaign"]);
        Assert.Equal(90m, completed["HandleMinutes"]);
        Assert.Equal(DateTimeKind.Utc, ((DateTime)completed["CreatedUtc"]).Kind);
        Assert.Null(rows[0]["Disposition"]);
    }

    [Fact]
    public async Task Activities_DateConditions_ReadOnlyThatRange_AndTruncate()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var query = Query("Activities");
        query.MaxRows = 2;
        query.Conditions =
        [
            new ReportDataCondition { Field = "CreatedUtc", Operator = ReportFilterOperator.Between, Values = [_day.AddDays(1), _day.AddDays(3)] },
        ];

        // Act
        var table = await source.QueryAsync(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["a3", "a2"], Rows(table).Select(row => row["ItemId"]));
        Assert.True(table.Truncated);
    }

    [Fact]
    public async Task Activities_JoinKeys_ReadOnlyTheMatchingActivities()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var query = Query("Activities");
        query.Conditions = [new ReportDataCondition { Field = "ItemId", Operator = ReportFilterOperator.In, Values = ["a1", "a3", "missing"], IsJoinKey = true }];
        var empty = Query("Activities");
        empty.Conditions = [new ReportDataCondition { Field = "AssignedToId", Operator = ReportFilterOperator.In, Values = [], IsJoinKey = true }];

        // Act
        var table = await source.QueryAsync(query, TestContext.Current.CancellationToken);
        var none = await source.QueryAsync(empty, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync("Activities", query.Context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["a3", "a1"], Rows(table).Select(row => row["ItemId"]));
        Assert.Empty(none.Rows);
        Assert.True(schema.FindField("AssignedToId").IsKeyFilterable);
        Assert.False(schema.FindField("Channel").IsKeyFilterable);
        Assert.Equal("CreatedUtc", schema.DataSet.DefaultDateField);
    }

    public static TheoryData<string> GroupedReports => new(GroupedQueries.Keys);

    private static Dictionary<string, Func<ReportQueryDefinition>> GroupedQueries { get; } = new(StringComparer.Ordinal)
    {
        ["Count by status and channel"] = () => Activities(Column("status", "A.Status"), Column("channel", "A.Channel"), Column("n", "$count")),
        ["Attempts by local day"] = () => Activities(
            Column("day", "A.CreatedUtc", transform: ReportFieldTransform.Day),
            Column("attempts", "A.Attempts", ReportAggregate.Sum),
            Column("average", "A.Attempts", ReportAggregate.Average),
            Column("first", "A.ScheduledUtc", ReportAggregate.Min)),
        ["By month, completed only"] = () => Filtered(
            Activities(Column("month", "A.CreatedUtc", transform: ReportFieldTransform.Month), Column("n", "$count")),
            new ReportFilterDefinition { Id = "done", Field = "A.Status", Stage = ReportFilterStage.Rows, Operator = ReportFilterOperator.Equals, Values = ["completed"] }),
        ["By agent, last days, not SMS"] = () => Filtered(
            Filtered(
                Activities(Column("agent", "A.AssignedToId"), Column("n", "$count"), Column("max", "A.Attempts", ReportAggregate.Max)),
                new ReportFilterDefinition { Id = "recent", Field = "A.CreatedUtc", Stage = ReportFilterStage.Rows, Operator = ReportFilterOperator.Between, Values = ["2026-05-02", "2026-05-04"] }),
            new ReportFilterDefinition { Id = "channel", Field = "A.Channel", Stage = ReportFilterStage.Rows, Operator = ReportFilterOperator.NotEquals, Values = ["sms"] }),
    };

    [Theory]
    [MemberData(nameof(GroupedReports))]
    public async Task Activities_GroupedInTheDatabase_GiveTheSameReportAsReadingRows(string report)
    {
        // Arrange
        await using (var seed = _store.CreateSession())
        {
            await seed.SaveAsync(new OmnichannelActivity { ItemId = "b1", Kind = ActivityKind.Sms, Channel = "Sms", Status = ActivityStatus.Completed, AssignedToId = "agent-2", Attempts = 7, CreatedUtc = _day.AddDays(2).AddHours(3), ScheduledUtc = _day }, false, OmnichannelConstants.CollectionName, TestContext.Current.CancellationToken);
            await seed.SaveAsync(new OmnichannelActivity { ItemId = "b2", Kind = ActivityKind.Call, Channel = OmnichannelConstants.Channels.Phone, Status = ActivityStatus.Completed, Attempts = 2, CreatedUtc = _day.AddMonths(1).AddHours(2), ScheduledUtc = _day }, false, OmnichannelConstants.CollectionName, TestContext.Current.CancellationToken);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var session = _store.CreateSession();
        var grouping = Source(session);
        var rowsOnly = new RowsOnly(grouping);

        // Act
        var expected = await ReportDesignerTestServices.Engine(rowsOnly).ExecuteAsync(GroupedQueries[report](), LocalContext(), TestContext.Current.CancellationToken);
        var actual = await ReportDesignerTestServices.Engine(grouping).ExecuteAsync(GroupedQueries[report](), LocalContext(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(actual.GroupedBySource, "The database should have grouped the report.");
        Assert.Equal(expected.Rows, actual.Rows);
        Assert.Equal(expected.Regroup([]).Select(group => group.Values), actual.Regroup([]).Select(group => group.Values));
    }

    [Fact]
    public async Task Activities_AComputedField_IsReadRowByRow()
    {
        // Arrange
        await using var session = _store.CreateSession();

        // Act: the disposition name is looked up, not stored in the index.
        var result = await ReportDesignerTestServices.Engine(Source(session)).ExecuteAsync(
            Activities(Column("disposition", "A.Disposition"), Column("n", "$count")),
            LocalContext(),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.GroupedBySource);
        Assert.Contains(result.Rows, row => (string)row[0] == "Sale");
    }

    [Fact]
    public async Task Dispositions_AreReadWithTheirOutcome()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);

        // Act
        var table = await source.QueryAsync(Query("Dispositions"), TestContext.Current.CancellationToken);

        // Assert
        var row = Assert.Single(Rows(table));
        Assert.Equal("d-sale", row["ItemId"]);
        Assert.Equal("Sale", row["Name"]);
        Assert.Equal(nameof(DispositionOutcome.NoAnswer), row["Outcome"]);
    }

    private static OmnichannelReportDataSource Source(ISession session, bool canViewReports = true)
    {
        var authorizationService = new Mock<IAuthorizationService>();
        authorizationService
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements) =>
                canViewReports && requirements.OfType<PermissionRequirement>().All(requirement => requirement.Permission.Name == OmnichannelConstants.Permissions.ViewReports.Name)
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed());

        var dispositions = new Mock<INamedCatalogManager<OmnichannelDisposition>>();
        dispositions
            .Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<IEnumerable<OmnichannelDisposition>>([new OmnichannelDisposition { ItemId = "d-sale", Name = "Sale", Outcome = DispositionOutcome.NoAnswer }]));

        var campaigns = new Mock<ICatalogManager<OmnichannelCampaign>>();
        campaigns
            .Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<IEnumerable<OmnichannelCampaign>>([new OmnichannelCampaign { ItemId = "c1", DisplayText = "Spring drive" }]));

        var groups = new Mock<ICatalogManager<OmnichannelCampaignGroup>>();
        groups
            .Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<IEnumerable<OmnichannelCampaignGroup>>([]));

        return new OmnichannelReportDataSource(
            session,
            dispositions.Object,
            campaigns.Object,
            groups.Object,
            authorizationService.Object,
            new ContentReportTestLocalizer<OmnichannelReportDataSource>());
    }

    // The tenant is five hours behind UTC, so local days differ from UTC ones.
    private static ReportQueryExecutionContext LocalContext()
    {
        var context = ReportDesignerTestServices.Context(_day.AddDays(4));

        context.DataSourceContext = new ReportDataSourceContext { User = _user };
        context.ToLocal = utc => DateTime.SpecifyKind(utc.AddHours(-5), DateTimeKind.Unspecified);
        context.ToUtc = local => DateTime.SpecifyKind(local.AddHours(5), DateTimeKind.Utc);

        return context;
    }

    private static ReportQueryDefinition Activities(params ReportColumnDefinition[] columns)
    {
        return new ReportQueryDefinition
        {
            DataSets = [new ReportDataSetReference { Alias = "A", Source = OmnichannelReportDataSource.SourceName, DataSet = "Activities" }],
            Columns = columns,
        };
    }

    private static ReportQueryDefinition Filtered(ReportQueryDefinition query, ReportFilterDefinition filter)
    {
        query.Filters.Add(filter);

        return query;
    }

    private static ReportColumnDefinition Column(string id, string field, ReportAggregate aggregate = ReportAggregate.None, ReportFieldTransform transform = ReportFieldTransform.None)
    {
        return ReportDesignerTestServices.Column(id, field, aggregate, transform);
    }

    // The same source without its grouping, so the engine reads rows.
    private sealed class RowsOnly : IReportDataSource
    {
        private readonly IReportDataSource _inner;

        public RowsOnly(IReportDataSource inner)
        {
            _inner = inner;
        }

        public string Name => _inner.Name;

        public LocalizedString DisplayName => _inner.DisplayName;

        public LocalizedString Description => _inner.Description;

        public Task<IReadOnlyList<ReportDataSetDescriptor>> GetDataSetsAsync(ReportDataSourceContext context, CancellationToken cancellationToken = default)
            => _inner.GetDataSetsAsync(context, cancellationToken);

        public Task<ReportDataSetSchema> GetSchemaAsync(string dataSet, ReportDataSourceContext context, CancellationToken cancellationToken = default)
            => _inner.GetSchemaAsync(dataSet, context, cancellationToken);

        public Task<ReportDataTable> QueryAsync(ReportDataSourceQuery query, CancellationToken cancellationToken = default)
            => _inner.QueryAsync(query, cancellationToken);
    }

    private static ReportDataSourceQuery Query(string dataSet)
    {
        return new ReportDataSourceQuery
        {
            DataSet = dataSet,
            MaxRows = 100,
            Context = new ReportDataSourceContext { User = _user },
        };
    }

    private static List<Dictionary<string, object>> Rows(ReportDataTable table)
    {
        return table.Rows
            .Select(row => table.Fields.Select((field, index) => (field.Name, Value: row[index])).ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal))
            .ToList();
    }
}

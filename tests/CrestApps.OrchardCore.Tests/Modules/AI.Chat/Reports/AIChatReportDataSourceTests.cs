using System.Security.Claims;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.AIChat;
using CrestApps.OrchardCore.AI.Chat.Reports;
using CrestApps.OrchardCore.AI.Chat.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Tests.Modules.Reports.Contents;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Security;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat.Reports;

/// <summary>
/// The AI chat data source reads real chat sessions and session metrics from a SQLite store, so the index queries,
/// the fields they expose, the date push-down, and the permission they require are all exercised end to end.
/// </summary>
public sealed class AIChatReportDataSourceTests : IAsyncLifetime
{
    private static readonly ClaimsPrincipal _user = ReportDesignerPrincipals.User("u1", "Analyst");
    private static readonly DateTime _start = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly IOptions<YesSqlStoreOptions> _storeOptions = Options.Create(new YesSqlStoreOptions());

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"ai-chat-reports-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var collection = _storeOptions.Value.AICollectionName;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_databasePath};Pooling=False"));
        _store.RegisterIndexes([new AIChatSessionIndexProvider(_storeOptions), new AIChatSessionMetricsIndexProvider(_storeOptions)], collection);

        await _store.InitializeAsync(cancellationToken);
        await _store.InitializeCollectionAsync(collection, cancellationToken);

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(cancellationToken);
            var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);

            await schemaBuilder.CreateAIChatSessionIndexSchemaAsync(_storeOptions.Value);
            await schemaBuilder.CreateAIChatSessionMetricsIndexTableAsync(_storeOptions.Value, new AIChatSessionMetricsIndexSchemaOptions());
            await transaction.CommitAsync(cancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            // Five sessions, one a day; the newest is still active.
            for (var day = 0; day < 5; day++)
            {
                var active = day == 4;

                await session.SaveAsync(new AIChatSession
                {
                    SessionId = $"session-{day}",
                    ProfileId = day % 2 == 0 ? "profile-support" : "profile-sales",
                    Title = $"Chat {day}",
                    UserId = $"user-{day}",
                    RemoteAddress = "203.0.113.7",
                    RemoteAddressHash = "remote-hash",
                    Status = active ? ChatSessionStatus.Active : ChatSessionStatus.Closed,
                    CreatedUtc = _start.AddDays(day),
                    LastActivityUtc = _start.AddDays(day).AddMinutes(30),
                    ClosedAtUtc = active ? null : _start.AddDays(day).AddMinutes(45),
                    ResponseHandlerName = active ? "live-agent" : null,
                    PostSessionProcessingStatus = active ? PostSessionProcessingStatus.None : PostSessionProcessingStatus.Completed,
                }, false, collection, cancellationToken);

                await session.SaveAsync(new AIChatSessionEvent
                {
                    SessionId = $"session-{day}",
                    ProfileId = day % 2 == 0 ? "profile-support" : "profile-sales",
                    VisitorId = $"visitor-{day}",
                    UserId = $"user-{day}",
                    IsAuthenticated = day % 2 == 0,
                    RemoteAddress = "203.0.113.7",
                    SessionStartedUtc = _start.AddDays(day),
                    SessionEndedUtc = active ? null : _start.AddDays(day).AddMinutes(45),
                    MessageCount = 4 + day,
                    HandleTimeSeconds = 90.5,
                    IsResolved = day % 2 == 0,
                    TotalInputTokens = 1000,
                    TotalOutputTokens = 250,
                    AverageResponseLatencyMs = 812.25,
                    CompletionCount = 2,
                    UserRating = day == 0 ? true : null,
                    ThumbsUpCount = 1,
                    ThumbsDownCount = 0,
                    ConversionScore = day == 0 ? 7 : null,
                    ConversionMaxScore = day == 0 ? 10 : null,
                    CreatedUtc = _start.AddDays(day),
                }, false, collection, cancellationToken);
            }

            await session.SaveChangesAsync(cancellationToken);
        }
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task WithoutViewChatAnalytics_NothingIsExposed()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session, canViewAnalytics: false);
        var context = new ReportDataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var sessionsSchema = await source.GetSchemaAsync(AIChatReportDataSource.ChatSessionsDataSet, context, TestContext.Current.CancellationToken);
        var metricsSchema = await source.GetSchemaAsync(AIChatReportDataSource.ChatSessionMetricsDataSet, context, TestContext.Current.CancellationToken);
        var table = await source.QueryAsync(Query(AIChatReportDataSource.ChatSessionsDataSet), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(sessionsSchema);
        Assert.Null(metricsSchema);
        Assert.Empty(table.Rows);
    }

    [Fact]
    public async Task WithoutAPrincipal_NothingIsExposed()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var context = new ReportDataSourceContext();

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync(AIChatReportDataSource.ChatSessionsDataSet, context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(schema);
    }

    [Fact]
    public async Task Schema_ChatSessions_ReferencesUsers_AndNeverExposesTheRemoteAddress()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var context = new ReportDataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync(AIChatReportDataSource.ChatSessionsDataSet, context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([AIChatReportDataSource.ChatSessionsDataSet, AIChatReportDataSource.ChatSessionMetricsDataSet], dataSets.Select(dataSet => dataSet.Name));
        Assert.True(schema.FindField(AIChatReportDataSource.SessionIdField).IsIdentifier);
        Assert.True(schema.FindField("ProfileId").IsIdentifier);

        var userReference = Assert.Single(schema.FindField(ReportsConstants.UserIdField).References);
        Assert.Equal(ReportsConstants.UsersDataSource, userReference.Source);
        Assert.Equal(ReportsConstants.UsersDataSet, userReference.DataSet);
        Assert.Equal(ReportsConstants.UserIdField, userReference.Field);
        Assert.Contains(schema.DataSet.References, reference => reference.Source == ReportsConstants.UsersDataSource);
        Assert.DoesNotContain(schema.Fields, field => field.Name.Contains("Remote", StringComparison.OrdinalIgnoreCase) ||
            field.Name.Contains("Address", StringComparison.OrdinalIgnoreCase) ||
            field.Name.Contains("ClientId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Query_ChatSessions_ReturnsTypedValues_NewestFirst()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);

        // Act
        var table = await source.QueryAsync(Query(AIChatReportDataSource.ChatSessionsDataSet), TestContext.Current.CancellationToken);

        // Assert
        var rows = Rows(table);
        Assert.Equal(["session-4", "session-3", "session-2", "session-1", "session-0"], rows.Select(row => row[AIChatReportDataSource.SessionIdField]));
        Assert.False(table.Truncated);

        var active = rows[0];
        Assert.Equal("profile-support", active["ProfileId"]);
        Assert.Equal("Support", active["ProfileName"]);
        Assert.Equal("Chat 4", active["Title"]);
        Assert.Equal("user-4", active[ReportsConstants.UserIdField]);
        Assert.Equal("Active", active["Status"]);
        Assert.Equal("live-agent", active["ResponseHandlerName"]);
        Assert.Equal("None", active["PostSessionProcessingStatus"]);
        Assert.Null(active["ClosedAtUtc"]);

        var closed = rows[1];
        Assert.Equal("Sales", closed["ProfileName"]);
        Assert.Equal("Closed", closed["Status"]);
        Assert.Equal("Completed", closed["PostSessionProcessingStatus"]);

        var lastActivity = Assert.IsType<DateTime>(closed[AIChatSessionsReportDataSet.LastActivityUtcField]);
        Assert.Equal(DateTimeKind.Utc, lastActivity.Kind);
        Assert.Equal(_start.AddDays(3).AddMinutes(30), lastActivity);
        Assert.Equal(DateTimeKind.Utc, Assert.IsType<DateTime>(closed["CreatedUtc"]).Kind);
        Assert.Equal(_start.AddDays(3).AddMinutes(45), closed["ClosedAtUtc"]);
    }

    [Fact]
    public async Task Query_ChatSessions_ComputesOnlyTheRequestedFields_AndSkipsProfileNamesWhenUnused()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var profileManager = ProfileManager();
        var source = Source(session, profileManager: profileManager.Object);

        // Act
        var table = await source.QueryAsync(Query(AIChatReportDataSource.ChatSessionsDataSet, AIChatReportDataSource.SessionIdField, "Status"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([AIChatReportDataSource.SessionIdField, "Status"], table.Fields.Select(field => field.Name));
        Assert.Equal(5, table.Rows.Count);
        profileManager.Verify(manager => manager.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Query_ChatSessions_DatePushDown_NeverDropsAMatchingRow()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var from = _start.AddDays(1).AddMinutes(30);
        var to = _start.AddDays(3).AddMinutes(30);
        var between = Query(AIChatReportDataSource.ChatSessionsDataSet, AIChatReportDataSource.SessionIdField, AIChatSessionsReportDataSet.LastActivityUtcField);
        between.Conditions.Add(Condition(AIChatSessionsReportDataSet.LastActivityUtcField, ReportFilterOperator.Between, from, to));
        var strict = Query(AIChatReportDataSource.ChatSessionsDataSet, AIChatReportDataSource.SessionIdField, AIChatSessionsReportDataSet.LastActivityUtcField);
        strict.Conditions.Add(Condition(AIChatSessionsReportDataSet.LastActivityUtcField, ReportFilterOperator.GreaterThan, from));
        var unrelated = Query(AIChatReportDataSource.ChatSessionsDataSet, AIChatReportDataSource.SessionIdField);
        unrelated.Conditions.Add(Condition("CreatedUtc", ReportFilterOperator.GreaterThan, _start.AddDays(10)));

        // Act
        var betweenRows = Rows(await source.QueryAsync(between, TestContext.Current.CancellationToken));
        var strictRows = Rows(await source.QueryAsync(strict, TestContext.Current.CancellationToken));
        var unrelatedRows = Rows(await source.QueryAsync(unrelated, TestContext.Current.CancellationToken));

        // Assert
        // The bounds are inclusive, so the rows on them stay; the report applies the strict comparison itself.
        Assert.Equal(["session-3", "session-2", "session-1"], betweenRows.Select(row => row[AIChatReportDataSource.SessionIdField]));
        Assert.Equal(["session-4", "session-3", "session-2", "session-1"], strictRows.Select(row => row[AIChatReportDataSource.SessionIdField]));
        Assert.Equal(5, unrelatedRows.Count);
    }

    [Fact]
    public async Task Query_ChatSessions_StopsAtMaxRows_AndSaysItWasTruncated()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var query = Query(AIChatReportDataSource.ChatSessionsDataSet, AIChatReportDataSource.SessionIdField);
        query.MaxRows = 2;

        // Act
        var table = await source.QueryAsync(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["session-4", "session-3"], Rows(table).Select(row => row[AIChatReportDataSource.SessionIdField]));
        Assert.True(table.Truncated);
    }

    [Fact]
    public async Task Query_ChatSessionMetrics_ReturnsTypedValues_AndReferencesChatSessions()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var context = new ReportDataSourceContext { User = _user };

        // Act
        var schema = await source.GetSchemaAsync(AIChatReportDataSource.ChatSessionMetricsDataSet, context, TestContext.Current.CancellationToken);
        var table = await source.QueryAsync(Query(AIChatReportDataSource.ChatSessionMetricsDataSet), TestContext.Current.CancellationToken);

        // Assert
        var sessionReference = Assert.Single(schema.FindField(AIChatReportDataSource.SessionIdField).References);
        Assert.Equal(AIChatReportDataSource.SourceName, sessionReference.Source);
        Assert.Equal(AIChatReportDataSource.ChatSessionsDataSet, sessionReference.DataSet);
        Assert.Equal(AIChatReportDataSource.SessionIdField, sessionReference.Field);
        Assert.Equal(ReportsConstants.UsersDataSet, Assert.Single(schema.FindField(ReportsConstants.UserIdField).References).DataSet);
        Assert.DoesNotContain(schema.Fields, field => field.Name.Contains("Remote", StringComparison.OrdinalIgnoreCase));

        var rows = Rows(table);
        Assert.Equal(["session-4", "session-3", "session-2", "session-1", "session-0"], rows.Select(row => row[AIChatReportDataSource.SessionIdField]));

        var oldest = rows[^1];
        Assert.Equal("Support", oldest["ProfileName"]);
        Assert.Equal("visitor-0", oldest["VisitorId"]);
        Assert.Equal(true, oldest["IsAuthenticated"]);
        Assert.Equal(true, oldest["IsResolved"]);
        Assert.Equal(4L, oldest["MessageCount"]);
        Assert.Equal(90.5m, oldest["HandleTimeSeconds"]);
        Assert.Equal(1000L, oldest["TotalInputTokens"]);
        Assert.Equal(250L, oldest["TotalOutputTokens"]);
        Assert.Equal(812.25m, oldest["AverageResponseLatencyMs"]);
        Assert.Equal(2L, oldest["CompletionCount"]);
        Assert.Equal(true, oldest["UserRating"]);
        Assert.Equal(1L, oldest["ThumbsUpCount"]);
        Assert.Equal(0L, oldest["ThumbsDownCount"]);
        Assert.Equal(7L, oldest["ConversionScore"]);
        Assert.Equal(10L, oldest["ConversionMaxScore"]);
        Assert.Equal(DateTimeKind.Utc, Assert.IsType<DateTime>(oldest[AIChatSessionMetricsReportDataSet.SessionStartedUtcField]).Kind);

        var newest = rows[0];
        Assert.Null(newest["SessionEndedUtc"]);
        Assert.Null(newest["UserRating"]);
        Assert.Null(newest["ConversionScore"]);
    }

    [Fact]
    public async Task Query_ChatSessionMetrics_DatePushDown_NeverDropsAMatchingRow()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var query = Query(AIChatReportDataSource.ChatSessionMetricsDataSet, AIChatReportDataSource.SessionIdField);
        query.Conditions.Add(Condition(AIChatSessionMetricsReportDataSet.SessionStartedUtcField, ReportFilterOperator.GreaterThanOrEqual, _start.AddDays(2)));
        query.Conditions.Add(Condition(AIChatSessionMetricsReportDataSet.SessionStartedUtcField, ReportFilterOperator.LessThan, _start.AddDays(4)));
        query.MaxRows = 1;

        // Act
        var table = await source.QueryAsync(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["session-4"], Rows(table).Select(row => row[AIChatReportDataSource.SessionIdField]));
        Assert.True(table.Truncated);

        query.MaxRows = 10;
        var all = Rows(await source.QueryAsync(query, TestContext.Current.CancellationToken));
        Assert.Equal(["session-4", "session-3", "session-2"], all.Select(row => row[AIChatReportDataSource.SessionIdField]));
    }

    [Fact]
    public async Task Startups_ListTheMetricsDataSet_OnlyWhenTheAnalyticsFeatureRegistersIt()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var withoutAnalytics = Services(session, analytics: false);
        var withAnalytics = Services(session, analytics: true);
        var context = new ReportDataSourceContext { User = _user };

        // Act
        var source = Assert.IsType<AIChatReportDataSource>(Assert.Single(withoutAnalytics.GetServices<IReportDataSource>()));
        var withoutAnalyticsDataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var hiddenSchema = await source.GetSchemaAsync(AIChatReportDataSource.ChatSessionMetricsDataSet, context, TestContext.Current.CancellationToken);
        var withAnalyticsDataSets = await withAnalytics.GetRequiredService<IReportDataSource>().GetDataSetsAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([AIChatReportDataSource.ChatSessionsDataSet], withoutAnalyticsDataSets.Select(dataSet => dataSet.Name));
        Assert.Null(hiddenSchema);
        Assert.Equal([AIChatReportDataSource.ChatSessionsDataSet, AIChatReportDataSource.ChatSessionMetricsDataSet], withAnalyticsDataSets.Select(dataSet => dataSet.Name));
    }

    private static ServiceProvider Services(ISession session, bool analytics)
    {
        var services = new ServiceCollection();

        services.AddSingleton(session);
        services.AddSingleton(ProfileManager().Object);
        services.AddSingleton(Authorization(canViewAnalytics: true));
        services.AddSingleton(_storeOptions);
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(ContentReportTestLocalizer<>));

        new CrestApps.OrchardCore.AI.Chat.ReportsStartup().ConfigureServices(services);

        if (analytics)
        {
            new CrestApps.OrchardCore.AI.Chat.ChatAnalyticsReportsStartup().ConfigureServices(services);
        }

        return services.BuildServiceProvider();
    }

    private static AIChatReportDataSource Source(ISession session, bool canViewAnalytics = true, IAIProfileManager profileManager = null)
    {
        var authorizationService = Authorization(canViewAnalytics);

        profileManager ??= ProfileManager().Object;

        return new AIChatReportDataSource(
            [
                new AIChatSessionsReportDataSet(session, profileManager, authorizationService, _storeOptions, new ContentReportTestLocalizer<AIChatSessionsReportDataSet>()),
                new AIChatSessionMetricsReportDataSet(session, profileManager, authorizationService, _storeOptions, new ContentReportTestLocalizer<AIChatSessionMetricsReportDataSet>()),
            ],
            new ContentReportTestLocalizer<AIChatReportDataSource>());
    }

    private static IAuthorizationService Authorization(bool canViewAnalytics)
    {
        var authorizationService = new Mock<IAuthorizationService>();

        authorizationService
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements) =>
                canViewAnalytics && requirements.OfType<PermissionRequirement>().All(requirement => requirement.Permission.Name == ChatAnalyticsPermissionProvider.ViewChatAnalytics.Name)
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed());

        return authorizationService.Object;
    }

    private static Mock<IAIProfileManager> ProfileManager()
    {
        var profileManager = new Mock<IAIProfileManager>();

        profileManager
            .Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIProfile[]
            {
                new() { ItemId = "profile-support", Name = "support", DisplayText = "Support" },
                new() { ItemId = "profile-sales", Name = "Sales" },
            });

        return profileManager;
    }

    private static ReportDataCondition Condition(string field, ReportFilterOperator filterOperator, params object[] values)
    {
        return new ReportDataCondition
        {
            Field = field,
            Operator = filterOperator,
            Values = values,
        };
    }

    private static ReportDataSourceQuery Query(string dataSet, params string[] fields)
    {
        return new ReportDataSourceQuery
        {
            DataSet = dataSet,
            Fields = new HashSet<string>(fields, StringComparer.Ordinal),
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

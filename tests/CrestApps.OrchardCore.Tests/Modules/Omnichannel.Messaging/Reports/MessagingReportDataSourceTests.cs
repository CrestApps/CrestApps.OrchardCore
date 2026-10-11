using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Indexes;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Reports;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Tests.Modules.Reports.Contents;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.Security;
using YesSql;
using YesSql.Indexes;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Messaging.Reports;

/// <summary>
/// The messaging data source reads real conversations and messages from a SQLite store, so the index queries, the
/// fields they expose, the date push-down, and the permission they require are all exercised end to end.
/// </summary>
public sealed class MessagingReportDataSourceTests : IAsyncLifetime
{
    private static readonly ClaimsPrincipal _user = ReportDesignerPrincipals.User("u1", "Supervisor");
    private static readonly DateTime _start = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"messaging-reports-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_databasePath};Pooling=False"));
        _store.RegisterIndexes([new MessagingConversationIndexProvider()], MessagingStorage.CollectionName);
        _store.RegisterIndexes([new TestOmnichannelMessageIndexProvider()], OmnichannelConstants.CollectionName);

        await _store.InitializeAsync(cancellationToken);
        await _store.InitializeCollectionAsync(MessagingStorage.CollectionName, cancellationToken);
        await _store.InitializeCollectionAsync(OmnichannelConstants.CollectionName, cancellationToken);

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(cancellationToken);
            var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);

            await schemaBuilder.CreateMapIndexTableAsync<MessagingConversationIndex>(table => table
                .Column<string>("ItemId", column => column.WithLength(26))
                .Column<string>("Channel", column => column.WithLength(MessagingStorage.ChannelLength))
                .Column<string>("ServiceAddress", column => column.WithLength(MessagingStorage.AddressLength))
                .Column<string>("ContactAddress", column => column.WithLength(MessagingStorage.AddressLength))
                .Column<string>("ContactContentItemId", column => column.WithLength(26))
                .Column<string>("CustomerKey", column => column.WithLength(MessagingStorage.CustomerKeyLength))
                .Column<string>("OwnerType", column => column.WithLength(32))
                .Column<string>("OwnerId", column => column.WithLength(26))
                .Column<string>("AssignedAgentId", column => column.WithLength(26))
                .Column<string>("AssignmentStatus", column => column.WithLength(32))
                .Column<string>("Status", column => column.WithLength(32))
                .Column<bool>("IsRead")
                .Column<DateTime>("LastMessageUtc", column => column.Nullable())
                .Column<int>("UnreadCount", column => column.NotNull().WithDefault(0))
                .Column<DateTime>("AssignedUtc", column => column.Nullable())
                .Column<DateTime>("FirstResponseDueUtc", column => column.Nullable()),
                collection: MessagingStorage.CollectionName);

            await schemaBuilder.CreateMapIndexTableAsync<OmnichannelMessageIndex>(table => table
                .Column<string>("Channel", column => column.WithLength(50))
                .Column<string>("CustomerAddress", column => column.WithLength(255))
                .Column<string>("ServiceAddress", column => column.WithLength(255))
                .Column<DateTime>("CreatedUtc", column => column.NotNull())
                .Column<bool>("IsInbound", column => column.NotNull().WithDefault(false))
                .Column<string>("ConversationId", column => column.WithLength(26))
                .Column<string>("ProviderMessageId", column => column.WithLength(128)),
                collection: OmnichannelConstants.CollectionName);

            await transaction.CommitAsync(cancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            // Four conversations, one a day, and one that has had no message yet.
            for (var day = 0; day < 4; day++)
            {
                var responded = day % 2 == 0;

                await session.SaveAsync(new MessagingConversation
                {
                    ItemId = $"conversation-{day}",
                    Channel = "Sms",
                    ServiceAddress = "+15550000000",
                    ContactAddress = $"+1555000010{day}",
                    OwnerType = ConversationOwnerType.Queue,
                    OwnerId = "queue-1",
                    ContactContentItemId = $"contact-{day}",
                    AssignedAgentId = "agent-1",
                    AssignmentStatus = ConversationAssignmentStatus.Assigned,
                    Status = day == 0 ? ConversationStatus.Closed : ConversationStatus.Open,
                    UnreadCount = day,
                    LastMessageUtc = _start.AddDays(day).AddMinutes(10),
                    LastMessagePreview = "a private message",
                    Summary = "a private summary",
                    AISessionId = day == 1 ? "ai-session-1" : null,
                    AssignedUtc = _start.AddDays(day).AddMinutes(1),
                    CreatedUtc = _start.AddDays(day),
                    FirstRespondedUtc = responded ? _start.AddDays(day).AddSeconds(90) : null,
                    FirstResponseDueUtc = responded ? null : _start.AddDays(day).AddMinutes(5),
                    FirstResponseBreached = day == 3,
                }, false, MessagingStorage.CollectionName, cancellationToken);
            }

            await session.SaveAsync(new MessagingConversation
            {
                ItemId = "conversation-silent",
                Channel = "Sms",
                ServiceAddress = "+15550000000",
                ContactAddress = "+15550000199",
                OwnerType = ConversationOwnerType.Personal,
                OwnerId = "agent-2",
                CreatedUtc = _start,
            }, false, MessagingStorage.CollectionName, cancellationToken);

            for (var minute = 0; minute < 3; minute++)
            {
                var inbound = minute != 1;

                await session.SaveAsync(new OmnichannelMessage
                {
                    Id = $"message-{minute}",
                    ConversationId = "conversation-1",
                    Channel = "Sms",
                    CustomerAddress = "+15550000101",
                    ServiceAddress = "+15550000000",
                    Content = minute == 1 ? "Hello there" : "Hi",
                    IsInbound = inbound,
                    SentByAgentId = inbound ? null : "agent-1",
                    DeliveryStatus = inbound ? null : "Delivered",
                    ErrorCode = minute == 2 ? "30003" : null,
                    MediaReferences = minute == 2 ? ["media-1", "media-2"] : [],
                    CreatedUtc = _start.AddDays(1).AddMinutes(minute),
                }, false, OmnichannelConstants.CollectionName, cancellationToken);
            }

            // A message of an automated activity belongs to no conversation, so it is not the workspace's to report.
            await session.SaveAsync(new OmnichannelMessage
            {
                Id = "message-automated",
                Channel = "Sms",
                Content = "Automated",
                CreatedUtc = _start.AddDays(2),
            }, false, OmnichannelConstants.CollectionName, cancellationToken);

            await session.SaveChangesAsync(cancellationToken);
        }
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task WithoutViewAllConversations_NothingIsExposed()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session, canViewAll: false);
        var context = new ReportDataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var conversationsSchema = await source.GetSchemaAsync(MessagingReportDataSource.ConversationsDataSet, context, TestContext.Current.CancellationToken);
        var messagesSchema = await source.GetSchemaAsync(MessagingReportDataSource.MessagesDataSet, context, TestContext.Current.CancellationToken);
        var table = await source.QueryAsync(Query(MessagingReportDataSource.ConversationsDataSet), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(conversationsSchema);
        Assert.Null(messagesSchema);
        Assert.Empty(table.Rows);
    }

    [Fact]
    public async Task Schema_Conversations_ReferencesUsersAndAIChat_AndNeverExposesMessageText()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var context = new ReportDataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync(MessagingReportDataSource.ConversationsDataSet, context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([MessagingReportDataSource.ConversationsDataSet, MessagingReportDataSource.MessagesDataSet], dataSets.Select(dataSet => dataSet.Name));
        Assert.True(schema.FindField(MessagingReportDataSource.ConversationIdField).IsIdentifier);
        Assert.True(schema.FindField("AssignedAgentId").IsIdentifier);

        var userReference = Assert.Single(schema.FindField("AssignedAgentUserId").References);
        Assert.Equal(ReportsConstants.UsersDataSource, userReference.Source);
        Assert.Equal(ReportsConstants.UsersDataSet, userReference.DataSet);
        Assert.Equal(ReportsConstants.UserIdField, userReference.Field);

        var sessionReference = Assert.Single(schema.FindField("AISessionId").References);
        Assert.Equal("AIChat", sessionReference.Source);
        Assert.Equal("ChatSessions", sessionReference.DataSet);
        Assert.Equal("SessionId", sessionReference.Field);
        Assert.DoesNotContain(schema.Fields, field => field.Name is "LastMessagePreview" or "Summary" or "History");
    }

    [Fact]
    public async Task Query_Conversations_ReturnsTypedValues_NewestFirst()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);

        // Act
        var table = await source.QueryAsync(Query(MessagingReportDataSource.ConversationsDataSet), TestContext.Current.CancellationToken);

        // Assert
        var rows = Rows(table);
        Assert.Equal(
            ["conversation-3", "conversation-2", "conversation-1", "conversation-0", "conversation-silent"],
            rows.Select(row => row[MessagingReportDataSource.ConversationIdField]));
        Assert.False(table.Truncated);

        var withAI = rows[2];
        Assert.Equal("Sms", withAI["Channel"]);
        Assert.Equal("+15550000101", withAI["ContactAddress"]);
        Assert.Equal("contact-1", withAI["ContactContentItemId"]);
        Assert.Equal("Queue", withAI["OwnerType"]);
        Assert.Equal("queue-1", withAI["OwnerId"]);
        Assert.Equal("Assigned", withAI["AssignmentStatus"]);
        Assert.Equal("Open", withAI["Status"]);
        Assert.Equal("agent-1", withAI["AssignedAgentId"]);
        Assert.Equal("user-agent-1", withAI["AssignedAgentUserId"]);
        Assert.Equal("Agent One", withAI["AssignedAgentName"]);
        Assert.Equal(1L, withAI["UnreadCount"]);
        Assert.Equal("ai-session-1", withAI["AISessionId"]);
        Assert.Null(withAI["FirstRespondedUtc"]);
        Assert.Null(withAI["FirstResponseSeconds"]);
        Assert.Equal(_start.AddDays(1).AddMinutes(5), withAI["FirstResponseDueUtc"]);

        var lastMessage = Assert.IsType<DateTime>(withAI[MessagingConversationsReportDataSet.LastMessageUtcField]);
        Assert.Equal(DateTimeKind.Utc, lastMessage.Kind);
        Assert.Equal(_start.AddDays(1).AddMinutes(10), lastMessage);

        var responded = rows[1];
        Assert.Equal(90m, responded["FirstResponseSeconds"]);
        Assert.Equal(false, responded["FirstResponseBreached"]);
        Assert.Equal(true, rows[0]["FirstResponseBreached"]);
        Assert.Equal("Closed", rows[3]["Status"]);

        var silent = rows[4];
        Assert.Null(silent[MessagingConversationsReportDataSet.LastMessageUtcField]);
        Assert.Null(silent["AssignedAgentUserId"]);
        Assert.Equal("Unassigned", silent["AssignmentStatus"]);
    }

    [Fact]
    public async Task Query_Conversations_DatePushDown_NeverDropsAMatchingRow()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var from = _start.AddDays(1).AddMinutes(10);
        var to = _start.AddDays(2).AddMinutes(10);
        var between = Query(MessagingReportDataSource.ConversationsDataSet, MessagingReportDataSource.ConversationIdField);
        between.Conditions.Add(Condition(MessagingConversationsReportDataSet.LastMessageUtcField, ReportFilterOperator.Between, from, to));
        var upper = Query(MessagingReportDataSource.ConversationsDataSet, MessagingReportDataSource.ConversationIdField);
        upper.Conditions.Add(Condition(MessagingConversationsReportDataSet.LastMessageUtcField, ReportFilterOperator.LessThan, from));

        // Act
        var betweenRows = Rows(await source.QueryAsync(between, TestContext.Current.CancellationToken));
        var upperRows = Rows(await source.QueryAsync(upper, TestContext.Current.CancellationToken));

        // Assert
        // The bounds are inclusive, so the rows on them stay; the report applies the strict comparison itself.
        Assert.Equal(["conversation-2", "conversation-1"], betweenRows.Select(row => row[MessagingReportDataSource.ConversationIdField]));
        Assert.Equal(["conversation-1", "conversation-0"], upperRows.Select(row => row[MessagingReportDataSource.ConversationIdField]));
    }

    [Fact]
    public async Task Query_Conversations_StopsAtMaxRows_AndDoesNotLoadAgentsWhenUnused()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var agents = AgentStore();
        var source = Source(session, agentProfileStore: agents.Object);
        var query = Query(MessagingReportDataSource.ConversationsDataSet, MessagingReportDataSource.ConversationIdField, "Status");
        query.MaxRows = 2;

        // Act
        var table = await source.QueryAsync(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["conversation-3", "conversation-2"], Rows(table).Select(row => row[MessagingReportDataSource.ConversationIdField]));
        Assert.True(table.Truncated);
        agents.Verify(store => store.GetAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Query_Messages_ReturnsConversationMessagesWithoutTheirText()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var context = new ReportDataSourceContext { User = _user };

        // Act
        var schema = await source.GetSchemaAsync(MessagingReportDataSource.MessagesDataSet, context, TestContext.Current.CancellationToken);
        var table = await source.QueryAsync(Query(MessagingReportDataSource.MessagesDataSet), TestContext.Current.CancellationToken);

        // Assert
        var conversationReference = Assert.Single(schema.FindField("ConversationId").References);
        Assert.Equal(MessagingReportDataSource.SourceName, conversationReference.Source);
        Assert.Equal(MessagingReportDataSource.ConversationsDataSet, conversationReference.DataSet);
        Assert.Equal(MessagingReportDataSource.ConversationIdField, conversationReference.Field);
        Assert.Equal(ReportsConstants.UsersDataSet, Assert.Single(schema.FindField("SentByAgentUserId").References).DataSet);
        Assert.DoesNotContain(schema.Fields, field => field.Name is "Content" or "Body" or "MediaReferences");

        var rows = Rows(table);
        Assert.Equal(["message-2", "message-1", "message-0"], rows.Select(row => row["Id"]));

        var outbound = rows[1];
        Assert.Equal("conversation-1", outbound["ConversationId"]);
        Assert.Equal(false, outbound["IsInbound"]);
        Assert.Equal(11L, outbound["Length"]);
        Assert.Equal("agent-1", outbound["SentByAgentId"]);
        Assert.Equal("user-agent-1", outbound["SentByAgentUserId"]);
        Assert.Equal("Agent One", outbound["SentByAgentName"]);
        Assert.Equal("Delivered", outbound["DeliveryStatus"]);
        Assert.Equal(DateTimeKind.Utc, Assert.IsType<DateTime>(outbound[MessagingMessagesReportDataSet.CreatedUtcField]).Kind);

        var withMedia = rows[0];
        Assert.Equal(true, withMedia["IsInbound"]);
        Assert.Equal(2L, withMedia["MediaCount"]);
        Assert.Equal("30003", withMedia["ErrorCode"]);
        Assert.Null(withMedia["SentByAgentName"]);
    }

    [Fact]
    public async Task Query_Messages_DatePushDown_NeverDropsAMatchingRow()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var query = Query(MessagingReportDataSource.MessagesDataSet, "Id");
        query.Conditions.Add(Condition(MessagingMessagesReportDataSet.CreatedUtcField, ReportFilterOperator.GreaterThan, _start.AddDays(1).AddMinutes(1)));

        // Act
        var rows = Rows(await source.QueryAsync(query, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["message-2", "message-1"], rows.Select(row => row["Id"]));
    }

    [Fact]
    public async Task ReportsStartup_RegistersTheDataSource()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var services = new ServiceCollection();

        services.AddSingleton(session);
        services.AddSingleton(AgentStore().Object);
        services.AddSingleton(Authorization(canViewAll: true));
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(ContentReportTestLocalizer<>));

        // Act
        new CrestApps.OrchardCore.Omnichannel.Messaging.ReportsStartup().ConfigureServices(services);

        await using var provider = services.BuildServiceProvider();
        var source = Assert.IsType<MessagingReportDataSource>(Assert.Single(provider.GetServices<IReportDataSource>()));
        var dataSets = await source.GetDataSetsAsync(new ReportDataSourceContext { User = _user }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(MessagingReportDataSource.SourceName, source.Name);
        Assert.Equal([MessagingReportDataSource.ConversationsDataSet, MessagingReportDataSource.MessagesDataSet], dataSets.Select(dataSet => dataSet.Name));
    }

    private static MessagingReportDataSource Source(ISession session, bool canViewAll = true, IAgentProfileStore agentProfileStore = null)
    {
        var authorizationService = Authorization(canViewAll);

        agentProfileStore ??= AgentStore().Object;

        return new MessagingReportDataSource(
            new MessagingConversationsReportDataSet(session, agentProfileStore, authorizationService, new ContentReportTestLocalizer<MessagingConversationsReportDataSet>()),
            new MessagingMessagesReportDataSet(session, agentProfileStore, authorizationService, new ContentReportTestLocalizer<MessagingMessagesReportDataSet>()),
            new ContentReportTestLocalizer<MessagingReportDataSource>());
    }

    private static IAuthorizationService Authorization(bool canViewAll)
    {
        var authorizationService = new Mock<IAuthorizationService>();

        authorizationService
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements) =>
                canViewAll && requirements.OfType<PermissionRequirement>().All(requirement => requirement.Permission.Name == MessagingPermissions.ViewAllConversations.Name)
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed());

        return authorizationService.Object;
    }

    private static Mock<IAgentProfileStore> AgentStore()
    {
        var agents = new Mock<IAgentProfileStore>();

        agents
            .Setup(store => store.GetAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> ids, CancellationToken cancellationToken) =>
                (IReadOnlyCollection<AgentProfile>)(ids.Contains("agent-1")
                    ? new[] { new AgentProfile { ItemId = "agent-1", UserId = "user-agent-1", UserName = "agent1", DisplayName = "Agent One" } }
                    : []));

        return agents;
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

    // Maps messages like the Omnichannel module's own index provider, which is internal to that module.
    private sealed class TestOmnichannelMessageIndexProvider : IndexProvider<OmnichannelMessage>
    {
        public TestOmnichannelMessageIndexProvider()
        {
            CollectionName = OmnichannelConstants.CollectionName;
        }

        public override void Describe(DescribeContext<OmnichannelMessage> context)
        {
            context
                .For<OmnichannelMessageIndex>()
                .Map(message => new OmnichannelMessageIndex
                {
                    Channel = message.Channel,
                    CustomerAddress = message.CustomerAddress,
                    ServiceAddress = message.ServiceAddress,
                    CreatedUtc = message.CreatedUtc,
                    IsInbound = message.IsInbound,
                    ConversationId = message.ConversationId,
                    ProviderMessageId = message.ProviderMessageId,
                });
        }
    }
}

using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.DataSources;
using CrestApps.OrchardCore.ContactCenter.Reports.DataSources.DataSets;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.DataSources;
using OrchardCore.Modules;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Reports;

/// <summary>
/// The Contact Center data sets read from its logs (agent state changes and dialer decisions from the event log, and
/// the pacing cycle records) and the campaign a queue item waits for, read from a SQLite store through their real
/// indexes and migrations, so the events each data set keeps, the values it reads from their payloads, the permission
/// it requires and what it pushes down to the store are exercised end to end.
/// </summary>
public sealed class ContactCenterLogReportDataSetTests : IAsyncLifetime
{
    private static readonly ClaimsPrincipal _user = ReportDesignerPrincipals.User("owner-1", "owner");

    private static readonly DateTime _first = new(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _second = new(2026, 9, 2, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _third = new(2026, 9, 3, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _fourth = new(2026, 9, 4, 9, 0, 0, DateTimeKind.Utc);

    private static readonly string _campaignQueueId = ContactCenterConstants.CampaignQueue.CreateId("campaign-1");

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"cc-log-report-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_databasePath};Pooling=False"));
        _store.RegisterIndexes(
        [
            new InteractionEventIndexProvider(),
            new DialerDecisionIndexProvider(),
            new DialerPacingCycleIndexProvider(),
            new QueueItemIndexProvider(),
        ]);

        await _store.InitializeAsync(cancellationToken);
        await _store.InitializeCollectionAsync(ContactCenterStorage.CollectionName, cancellationToken);

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(cancellationToken);
            var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);
            var events = new InteractionEventIndexMigrations(_store) { SchemaBuilder = schemaBuilder };
            var queueItems = new QueueItemIndexMigrations(_store, new FixedClock(_fourth)) { SchemaBuilder = schemaBuilder };

            await events.CreateAsync();
            await events.UpdateFrom7Async();
            await new DialerDecisionIndexMigrations { SchemaBuilder = schemaBuilder }.CreateAsync();
            await new DialerPacingCycleIndexMigrations { SchemaBuilder = schemaBuilder }.CreateAsync();
            await queueItems.CreateAsync();
            await queueItems.UpdateFrom6Async();
            await queueItems.UpdateFrom7Async();
            await transaction.CommitAsync(cancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            // Saved out of date order, so reading newest first is the store's doing, not the insertion order's.
            InteractionEvent[] events =
            [
                StateChange("state-audit", "agent-1", _second),
                SignedIn("state-signed-in", "agent-1", _first),
                SignedOut("state-signed-out", "agent-2", _fourth),
                Unreadable("state-unreadable", "agent-2", _third),
                Event("state-other-type", ContactCenterConstants.Events.AgentConnected, nameof(AgentProfile), "agent-1", _third),
                Event("state-other-aggregate", ContactCenterConstants.Events.AgentStateChanged, nameof(Interaction), "interaction-1", _third),
                Routing("decision-assigned", "queue-1", "queue-item-1", "activity-1", _first, succeeded: true, selectedAgentId: "agent-1"),
                Routing("decision-repeated", _campaignQueueId, "queue-item-2", "activity-2", _second, occurrenceCount: 5, lastSeenUtc: _fourth),
                Suppression("decision-suppressed", "activity-3", _third, DialerSuppressionReason.OutsideCallingWindow, "campaign-1", _campaignQueueId),
            ];

            foreach (var interactionEvent in events)
            {
                await session.SaveAsync(interactionEvent, collection: ContactCenterStorage.CollectionName, cancellationToken: cancellationToken);
            }

            await session.SaveAsync(PacingCycle("cycle-placed", DialerPacingCycleOutcome.Placed, _second, callsPlaced: 2), collection: ContactCenterStorage.CollectionName, cancellationToken: cancellationToken);
            await session.SaveAsync(PacingCycle("cycle-no-agents", DialerPacingCycleOutcome.NoAgents, _third, callsPlaced: 0), collection: ContactCenterStorage.CollectionName, cancellationToken: cancellationToken);
            await session.SaveAsync(PacingCycle("cycle-first", DialerPacingCycleOutcome.NothingDue, _first, callsPlaced: 0), collection: ContactCenterStorage.CollectionName, cancellationToken: cancellationToken);

            await session.SaveAsync(NewQueueItem("queue-item-campaign", _campaignQueueId, "activity-2", _second), collection: ContactCenterStorage.CollectionName, cancellationToken: cancellationToken);
            await session.SaveAsync(NewQueueItem("queue-item-inbound", "queue-1", "activity-1", _first), collection: ContactCenterStorage.CollectionName, cancellationToken: cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

        return ValueTask.CompletedTask;
    }

    [Theory]
    [InlineData(ContactCenterReportDataSets.AgentStateChanges)]
    [InlineData(ContactCenterReportDataSets.DialerDecisions)]
    [InlineData(ContactCenterReportDataSets.DialerPacingCycles)]
    public async Task WithoutViewReportsOrAUser_TheDataSetIsHidden_AndReadsNothing(string dataSet)
    {
        // Arrange
        await using var session = _store.CreateSession();
        var denied = Source(session, allowedPermissions: []);
        var allowed = Source(session);
        var withoutUser = new DataSourceContext();

        // Act
        var deniedDataSets = await denied.GetDataSetsAsync(new DataSourceContext { User = _user }, TestContext.Current.CancellationToken);
        var deniedSchema = await denied.GetSchemaAsync(dataSet, new DataSourceContext { User = _user }, TestContext.Current.CancellationToken);
        var deniedRows = await ReportDesignerTestServices.ReadAsync(denied, Query(dataSet, ContactCenterReportDataSets.ItemIdField), TestContext.Current.CancellationToken);
        var anonymousDataSets = await allowed.GetDataSetsAsync(withoutUser, TestContext.Current.CancellationToken);
        var anonymousSchema = await allowed.GetSchemaAsync(dataSet, withoutUser, TestContext.Current.CancellationToken);
        var allowedDataSets = await allowed.GetDataSetsAsync(new DataSourceContext { User = _user }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(deniedDataSets);
        Assert.Null(deniedSchema);
        Assert.Empty(deniedRows.Rows);
        Assert.Empty(anonymousDataSets);
        Assert.Null(anonymousSchema);
        Assert.Contains(allowedDataSets, descriptor => descriptor.Name == dataSet);
    }

    [Fact]
    public async Task AgentStateChanges_ReadOnlyTheAgentStateEvents_NewestFirst()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);

        // Act
        var table = await ReportDesignerTestServices.ReadAsync(source, Query(ContactCenterReportDataSets.AgentStateChanges, ContactCenterReportDataSets.ItemIdField), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ["state-signed-out", "state-unreadable", "state-audit", "state-signed-in"],
            Rows(table).Select(row => row[ContactCenterReportDataSets.ItemIdField]));
    }

    [Fact]
    public async Task AgentStateChanges_ReadTheStateAudit_AndTheOlderPresenceEvents()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);

        // Act
        var rows = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(ContactCenterReportDataSets.AgentStateChanges), TestContext.Current.CancellationToken))
            .ToDictionary(row => (string)row[ContactCenterReportDataSets.ItemIdField], StringComparer.Ordinal);

        // Assert
        var audit = rows["state-audit"];
        Assert.Equal(ContactCenterConstants.Events.AgentStateChanged, audit["EventType"]);
        Assert.Equal(true, audit["FromAudit"]);
        Assert.Equal("agent-1", audit["AgentId"]);
        Assert.Equal("user-1", audit["UserId"]);
        Assert.Equal("Available", audit["PreviousState"]);
        Assert.Equal("Break", audit["CurrentState"]);
        Assert.Null(audit["RequestedState"]);
        Assert.Equal("reason-lunch", audit["ReasonCodeId"]);
        Assert.Equal("Lunch", audit["Reason"]);
        Assert.Equal("Agent", audit["Source"]);
        Assert.Equal("Agent", audit["ActorType"]);
        Assert.Equal("session-1", audit["AgentSessionId"]);
        Assert.Equal("queue-1, queue-2", audit["QueueIds"]);
        Assert.Null(audit["CampaignIds"]);

        var changed = Assert.IsType<DateTime>(audit["ChangedUtc"]);
        Assert.Equal(DateTimeKind.Utc, changed.Kind);
        Assert.Equal(_second.AddSeconds(-2), changed);
        Assert.Equal(_second, audit["OccurredUtc"]);

        var signedIn = rows["state-signed-in"];
        Assert.Equal(false, signedIn["FromAudit"]);
        Assert.Equal("Offline", signedIn["PreviousState"]);
        Assert.Equal("Available", signedIn["CurrentState"]);
        Assert.Equal("queue-1", signedIn["QueueIds"]);

        // A sign-out is a change to Offline, whatever presence the event carried.
        Assert.Equal("Offline", rows["state-signed-out"]["CurrentState"]);

        // A payload written by a newer release is never read; the event's own values still are.
        var unreadable = rows["state-unreadable"];
        Assert.Equal("agent-2", unreadable["AgentId"]);
        Assert.Equal(_third, unreadable["OccurredUtc"]);
        Assert.Null(unreadable["CurrentState"]);
        Assert.Null(unreadable["ChangedUtc"]);
    }

    [Fact]
    public async Task AgentStateChanges_DatesAndAgentKeys_NeverDropAMatchingRow()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var between = Query(ContactCenterReportDataSets.AgentStateChanges, ContactCenterReportDataSets.ItemIdField);
        between.Conditions.Add(new DataCondition { Field = "OccurredUtc", Operator = DataFilterOperator.Between, Values = [_second, _third] });
        var agent = Query(ContactCenterReportDataSets.AgentStateChanges, ContactCenterReportDataSets.ItemIdField);
        agent.Conditions.Add(new DataCondition { Field = "AgentId", Operator = DataFilterOperator.In, Values = ["agent-1"], IsJoinKey = true });
        var none = Query(ContactCenterReportDataSets.AgentStateChanges, ContactCenterReportDataSets.ItemIdField);
        none.Conditions.Add(new DataCondition { Field = "AgentId", Operator = DataFilterOperator.In, Values = [], IsJoinKey = true });
        var eventType = Query(ContactCenterReportDataSets.AgentStateChanges, ContactCenterReportDataSets.ItemIdField);
        eventType.Conditions.Add(new DataCondition { Field = "EventType", Operator = DataFilterOperator.Equals, Values = ["agentsignedout"] });
        var schema = await source.GetSchemaAsync(ContactCenterReportDataSets.AgentStateChanges, new DataSourceContext { User = _user }, TestContext.Current.CancellationToken);

        // Act
        var betweenRows = Rows(await ReportDesignerTestServices.ReadAsync(source, between, TestContext.Current.CancellationToken));
        var agentRows = Rows(await ReportDesignerTestServices.ReadAsync(source, agent, TestContext.Current.CancellationToken));
        var noRows = Rows(await ReportDesignerTestServices.ReadAsync(source, none, TestContext.Current.CancellationToken));
        var eventTypeRows = Rows(await ReportDesignerTestServices.ReadAsync(source, eventType, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["state-unreadable", "state-audit"], betweenRows.Select(row => row[ContactCenterReportDataSets.ItemIdField]));
        Assert.Equal(["state-audit", "state-signed-in"], agentRows.Select(row => row[ContactCenterReportDataSets.ItemIdField]));
        Assert.Empty(noRows);

        // Text compares ignoring case, so the store must not drop the event a differently cased filter keeps.
        Assert.Equal(["state-signed-out"], eventTypeRows.Select(row => row[ContactCenterReportDataSets.ItemIdField]));
        Assert.True(schema.FindField("AgentId").IsKeyFilterable);
        Assert.False(schema.FindField("InteractionId").IsKeyFilterable);
        Assert.Equal("OccurredUtc", schema.DataSet.DefaultDateField);
    }

    [Fact]
    public async Task AgentStateChanges_AreNotGroupedInTheDatabase()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);

        // Act
        var groups = await ((IDataAggregateSource)source).AggregateAsync(CountBy(ContactCenterReportDataSets.AgentStateChanges, "EventType"), TestContext.Current.CancellationToken);

        // Assert
        // The event index holds every event, so a count there would not be a count of state changes.
        Assert.Null(groups);
    }

    [Fact]
    public async Task DialerDecisions_ReadTheDecisionLog_NewestLastMadeFirst_WithTheValuesTheIndexHolds()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);

        // Act
        var rows = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(ContactCenterReportDataSets.DialerDecisions), TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["decision-repeated", "decision-suppressed", "decision-assigned"], rows.Select(row => row[ContactCenterReportDataSets.ItemIdField]));

        var repeated = rows[0];
        Assert.Equal(ContactCenterConstants.Events.RoutingDecisionMade, repeated["EventType"]);
        Assert.Equal(false, repeated["Succeeded"]);
        Assert.Equal("No eligible agent", repeated["Reason"]);
        Assert.Equal(5L, repeated["OccurrenceCount"]);
        Assert.Equal(_campaignQueueId, repeated["QueueId"]);
        Assert.Equal("campaign-1", repeated["CampaignId"]);
        Assert.Equal("activity-2", repeated["ActivityItemId"]);
        Assert.Equal("queue-item-2", repeated["QueueItemId"]);
        Assert.Equal(_second, repeated["OccurredUtc"]);
        Assert.Equal(_fourth, repeated["LastSeenUtc"]);

        var suppressed = rows[1];
        Assert.Equal(ContactCenterConstants.Events.DialSuppressed, suppressed["EventType"]);
        Assert.Equal(nameof(DialerSuppressionReason.OutsideCallingWindow), suppressed["Reason"]);
        Assert.Equal("campaign-1", suppressed["CampaignId"]);
        Assert.Equal(1L, suppressed["OccurrenceCount"]);

        var assigned = rows[2];
        Assert.Equal(true, assigned["Succeeded"]);
        Assert.Equal("agent-1", assigned["AgentId"]);
        Assert.Null(assigned["CampaignId"]);
    }

    [Fact]
    public async Task DialerDecisions_DatesAndKeys_NeverDropAMatchingRow()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var between = Query(ContactCenterReportDataSets.DialerDecisions, ContactCenterReportDataSets.ItemIdField);
        between.Conditions.Add(new DataCondition { Field = "LastSeenUtc", Operator = DataFilterOperator.Between, Values = [_first, _third] });
        var campaign = Query(ContactCenterReportDataSets.DialerDecisions, ContactCenterReportDataSets.ItemIdField);
        campaign.Conditions.Add(new DataCondition { Field = "CampaignId", Operator = DataFilterOperator.In, Values = ["campaign-1"], IsJoinKey = true });
        var activity = Query(ContactCenterReportDataSets.DialerDecisions, ContactCenterReportDataSets.ItemIdField);
        activity.Conditions.Add(new DataCondition { Field = "ActivityItemId", Operator = DataFilterOperator.In, Values = ["activity-1", "missing"], IsJoinKey = true });

        // Act
        var betweenRows = Rows(await ReportDesignerTestServices.ReadAsync(source, between, TestContext.Current.CancellationToken));
        var campaignRows = Rows(await ReportDesignerTestServices.ReadAsync(source, campaign, TestContext.Current.CancellationToken));
        var activityRows = Rows(await ReportDesignerTestServices.ReadAsync(source, activity, TestContext.Current.CancellationToken));

        // Assert
        // The repeated decision was last made after the range, so the range, which is on the last time, leaves it out.
        Assert.Equal(["decision-suppressed", "decision-assigned"], betweenRows.Select(row => row[ContactCenterReportDataSets.ItemIdField]));
        Assert.Equal(["decision-repeated", "decision-suppressed"], campaignRows.Select(row => row[ContactCenterReportDataSets.ItemIdField]));
        Assert.Equal(["decision-assigned"], activityRows.Select(row => row[ContactCenterReportDataSets.ItemIdField]));
    }

    [Fact]
    public async Task DialerDecisions_AreGroupedInTheDatabase_AsReadingTheRowsWould()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var query = CountBy(ContactCenterReportDataSets.DialerDecisions, "EventType");
        query.Measures = [.. query.Measures, new DataAggregateMeasure { Function = DataAggregate.Sum, Field = "OccurrenceCount" }];

        // Act
        var groups = await ((IDataAggregateSource)source).AggregateAsync(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(groups);
        var byType = groups.ToDictionary(row => (string)row[0], StringComparer.Ordinal);
        Assert.Equal(2L, byType[ContactCenterConstants.Events.RoutingDecisionMade][1]);
        Assert.Equal(6L, Convert.ToInt64(byType[ContactCenterConstants.Events.RoutingDecisionMade][2]));
        Assert.Equal(1L, byType[ContactCenterConstants.Events.DialSuppressed][1]);
    }

    [Fact]
    public async Task DialerPacingCycles_ReadNewestCompletedFirst_WithTypedValues_AndPushDownTheOutcome()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var outcome = Query(ContactCenterReportDataSets.DialerPacingCycles, ContactCenterReportDataSets.ItemIdField);
        outcome.Conditions.Add(new DataCondition { Field = "Outcome", Operator = DataFilterOperator.In, Values = ["placed"] });

        // Act
        var rows = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(ContactCenterReportDataSets.DialerPacingCycles), TestContext.Current.CancellationToken));
        var outcomeRows = Rows(await ReportDesignerTestServices.ReadAsync(source, outcome, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["cycle-no-agents", "cycle-placed", "cycle-first"], rows.Select(row => row[ContactCenterReportDataSets.ItemIdField]));

        var placed = rows[1];
        Assert.Equal("Placed", placed["Outcome"]);
        Assert.Equal(2L, placed["CallsPlaced"]);
        Assert.Equal(_campaignQueueId, placed["QueueId"]);
        Assert.Equal("campaign-1", placed["CampaignId"]);
        Assert.Equal("profile-1", placed["DialerProfileId"]);
        Assert.Equal(30m, placed["DurationSeconds"]);
        Assert.Equal(_second, placed["CompletedUtc"]);

        Assert.Equal(["cycle-placed"], outcomeRows.Select(row => row[ContactCenterReportDataSets.ItemIdField]));
    }

    [Fact]
    public async Task QueueItems_NameTheCampaignOfACampaignQueue()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);

        // Act
        var schema = await source.GetSchemaAsync(ContactCenterReportDataSets.QueueItems, new DataSourceContext { User = _user }, TestContext.Current.CancellationToken);
        var rows = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(ContactCenterReportDataSets.QueueItems, ContactCenterReportDataSets.ItemIdField, "CampaignId"), TestContext.Current.CancellationToken));

        // Assert
        var reference = Assert.Single(schema.FindField("CampaignId").References);
        Assert.Equal(ContactCenterReportDataSets.OmnichannelSource, reference.Source);
        Assert.Equal(ContactCenterReportDataSets.OmnichannelCampaigns, reference.DataSet);
        Assert.Equal("campaign-1", rows.Single(row => (string)row[ContactCenterReportDataSets.ItemIdField] == "queue-item-campaign")["CampaignId"]);
        Assert.Null(rows.Single(row => (string)row[ContactCenterReportDataSets.ItemIdField] == "queue-item-inbound")["CampaignId"]);
    }

    [Fact]
    public async Task ReportIndexMigrations_CanRunAgain()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);

        // Act
        var decisions = await new DialerDecisionIndexMigrations { SchemaBuilder = schemaBuilder }.UpdateFrom1Async();
        var cycles = await new DialerPacingCycleIndexMigrations { SchemaBuilder = schemaBuilder }.UpdateFrom1Async();

        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, decisions);
        Assert.Equal(2, cycles);
    }

    [Fact]
    public async Task Startups_RegisterTheLogDataSets_WithTheFeaturesThatWriteThem()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var services = new ServiceCollection();

        services.AddSingleton<ISession>(session);
        services.AddSingleton<IInteractionEventUpcastService>(new DefaultInteractionEventUpcastService([]));
        services.AddSingleton(Authorization([ContactCenterPermissions.ViewReports]));
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>));

        // Act
        new DataSourcesStartup().ConfigureServices(services);
        new QueuesDataSourcesStartup().ConfigureServices(services);
        new AgentServicesDataSourcesStartup().ConfigureServices(services);
        new AgentsDataSourcesStartup().ConfigureServices(services);
        new DialerPacedDataSourcesStartup().ConfigureServices(services);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var source = scope.ServiceProvider.GetRequiredService<ContactCenterReportDataSource>();
        var dataSets = await source.GetDataSetsAsync(new DataSourceContext { User = _user }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(dataSets, dataSet => dataSet.Name == ContactCenterReportDataSets.AgentStateChanges);
        Assert.Contains(dataSets, dataSet => dataSet.Name == ContactCenterReportDataSets.DialerDecisions);
        Assert.Contains(dataSets, dataSet => dataSet.Name == ContactCenterReportDataSets.DialerPacingCycles);
        Assert.Equal(ContactCenterConstants.Feature.DialerPaced, typeof(DialerPacedDataSourcesStartup).GetCustomAttributes(typeof(FeatureAttribute), false).Cast<FeatureAttribute>().Single().FeatureName);
    }

    private static ContactCenterReportDataSource Source(ISession session, Permission[] allowedPermissions = null)
    {
        var authorizationService = Authorization(allowedPermissions ?? [ContactCenterPermissions.ViewReports]);

        IContactCenterReportDataSet[] dataSets =
        [
            new AgentStateChangeReportDataSet(session, new DefaultInteractionEventUpcastService([]), authorizationService, new PassThroughStringLocalizer<AgentStateChangeReportDataSet>()),
            new DialerDecisionReportDataSet(session, authorizationService, new PassThroughStringLocalizer<DialerDecisionReportDataSet>()),
            new DialerPacingCycleReportDataSet(session, authorizationService, new PassThroughStringLocalizer<DialerPacingCycleReportDataSet>()),
            new QueueItemReportDataSet(session, authorizationService, new PassThroughStringLocalizer<QueueItemReportDataSet>()),
        ];

        return new ContactCenterReportDataSource(dataSets);
    }

    private static IAuthorizationService Authorization(IEnumerable<Permission> allowedPermissions)
    {
        var allowed = allowedPermissions.Select(permission => permission.Name).ToHashSet(StringComparer.Ordinal);
        var authorizationService = new Mock<IAuthorizationService>();

        authorizationService
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements) =>
                user is not null && requirements.OfType<PermissionRequirement>().All(requirement => allowed.Contains(requirement.Permission.Name))
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed());

        return authorizationService.Object;
    }

    private static InteractionEvent Event(string id, string eventType, string aggregateType, string aggregateId, DateTime occurredUtc)
    {
        return new InteractionEvent
        {
            ItemId = id,
            EventType = eventType,
            AggregateType = aggregateType,
            AggregateId = aggregateId,
            OccurredUtc = occurredUtc,
            RecordedUtc = occurredUtc,
            IdempotencyKey = id,
        };
    }

    private static InteractionEvent StateChange(string id, string agentId, DateTime occurredUtc)
    {
        var interactionEvent = Event(id, ContactCenterConstants.Events.AgentStateChanged, nameof(AgentProfile), agentId, occurredUtc);
        interactionEvent.ActorType = ContactCenterActorType.Agent;
        interactionEvent.ActorId = "user-1";

        interactionEvent.SetData(new AgentStateChangedEventData
        {
            AgentId = agentId,
            UserId = "user-1",
            PreviousState = AgentPresenceStatus.Available,
            CurrentState = AgentPresenceStatus.Break,
            ReasonCodeId = "reason-lunch",
            ReasonName = "Lunch",
            Source = "Agent",
            AgentSessionId = "session-1",
            QueueIds = ["queue-1", "queue-2"],
            ChangedUtc = occurredUtc.AddSeconds(-2),
        });

        return interactionEvent;
    }

    private static InteractionEvent SignedIn(string id, string agentId, DateTime occurredUtc)
    {
        var interactionEvent = Event(id, ContactCenterConstants.Events.AgentSignedIn, nameof(AgentProfile), agentId, occurredUtc);

        interactionEvent.SetData(new AgentPresenceChangedEventData
        {
            AgentId = agentId,
            UserId = "user-1",
            PreviousStatus = AgentPresenceStatus.Offline,
            CurrentStatus = AgentPresenceStatus.Available,
            QueueIds = ["queue-1"],
            ChangedUtc = occurredUtc,
        });

        return interactionEvent;
    }

    private static InteractionEvent SignedOut(string id, string agentId, DateTime occurredUtc)
    {
        var interactionEvent = Event(id, ContactCenterConstants.Events.AgentSignedOut, nameof(AgentProfile), agentId, occurredUtc);

        interactionEvent.SetData(new AgentPresenceChangedEventData
        {
            AgentId = agentId,
            UserId = "user-2",
            PreviousStatus = AgentPresenceStatus.Available,
            CurrentStatus = AgentPresenceStatus.Available,
            ChangedUtc = occurredUtc,
        });

        return interactionEvent;
    }

    private static InteractionEvent Unreadable(string id, string agentId, DateTime occurredUtc)
    {
        var interactionEvent = StateChange(id, agentId, occurredUtc);
        interactionEvent.SchemaVersion = ContactCenterStorage.CurrentEventSchemaVersion + 1;

        return interactionEvent;
    }

    private static InteractionEvent Routing(
        string id,
        string queueId,
        string queueItemId,
        string activityItemId,
        DateTime occurredUtc,
        bool succeeded = false,
        string selectedAgentId = null,
        int occurrenceCount = 1,
        DateTime? lastSeenUtc = null)
    {
        var interactionEvent = Event(id, ContactCenterConstants.Events.RoutingDecisionMade, nameof(QueueItem), queueItemId, occurredUtc);

        interactionEvent.SetData(new ActivityRoutingDecisionEventData
        {
            QueueId = queueId,
            QueueItemId = queueItemId,
            ActivityItemId = activityItemId,
            Succeeded = succeeded,
            SelectedAgentId = selectedAgentId,
            Reason = succeeded ? "Assigned" : "No eligible agent",
            OccurrenceCount = occurrenceCount,
            LastSeenUtc = lastSeenUtc,
        });

        return interactionEvent;
    }

    private static InteractionEvent Suppression(
        string id,
        string activityItemId,
        DateTime occurredUtc,
        DialerSuppressionReason reason,
        string campaignId,
        string queueId)
    {
        var interactionEvent = Event(id, ContactCenterConstants.Events.DialSuppressed, "OmnichannelActivity", activityItemId, occurredUtc);

        interactionEvent.SetData(new DialerSuppressionEventData
        {
            ProfileItemId = "profile-1",
            ActivityItemId = activityItemId,
            Reason = reason,
            Description = "Suppressed by the compliance gate.",
            Destination = "+15550000001",
            CampaignId = campaignId,
            QueueId = queueId,
        });

        return interactionEvent;
    }

    private static DialerPacingCycle PacingCycle(string id, DialerPacingCycleOutcome outcome, DateTime completedUtc, int callsPlaced)
    {
        return new DialerPacingCycle
        {
            ItemId = id,
            QueueId = _campaignQueueId,
            CampaignId = "campaign-1",
            DialerProfileId = "profile-1",
            Outcome = outcome,
            CallsPlaced = callsPlaced,
            FirstStartedUtc = completedUtc.AddSeconds(-30),
            StartedUtc = completedUtc.AddSeconds(-30),
            CompletedUtc = completedUtc,
        };
    }

    private static QueueItem NewQueueItem(string id, string queueId, string activityItemId, DateTime enqueuedUtc)
    {
        return new QueueItem
        {
            ItemId = id,
            QueueId = queueId,
            ActivityItemId = activityItemId,
            EnqueuedUtc = enqueuedUtc,
            QueueEnteredUtc = enqueuedUtc,
        };
    }

    private static DataAggregateQuery CountBy(string dataSet, string field)
    {
        return new DataAggregateQuery
        {
            DataSet = dataSet,
            Groups = [new DataAggregateGroup { Field = field }],
            Measures = [new DataAggregateMeasure { Function = DataAggregate.Count }],
            Context = new DataSourceContext { User = _user },
        };
    }

    private static DataSourceQuery Query(string dataSet, params string[] fields)
    {
        return new DataSourceQuery
        {
            DataSet = dataSet,
            Fields = new HashSet<string>(fields, StringComparer.Ordinal),
            MaxRows = 100,
            Context = new DataSourceContext { User = _user },
        };
    }

    private static List<Dictionary<string, object>> Rows(DataRowSet table)
    {
        return table.Rows
            .Select(row => table.Fields.Select((field, index) => (field.Name, Value: row[index])).ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal))
            .ToList();
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; }

        public ITimeZone GetTimeZone(string timeZoneId) => throw new NotSupportedException();

        public ITimeZone GetSystemTimeZone() => throw new NotSupportedException();

        public ITimeZone[] GetTimeZones() => [];

        public DateTimeOffset ConvertToTimeZone(DateTimeOffset dateTimeOffset, ITimeZone timeZone) => dateTimeOffset;
    }
}

using System.Reflection;
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
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Tests.Modules.Reports.Contents;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.Modules;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Reports;

/// <summary>
/// The Contact Center data source reads real Contact Center documents from a SQLite store through their real indexes
/// and migrations, so the permission it requires, the values it exposes, and the date range it pushes down to the
/// store are all exercised end to end.
/// </summary>
public sealed class ContactCenterReportDataSourceTests : IAsyncLifetime
{
    private static readonly ClaimsPrincipal _user = ReportDesignerPrincipals.User("owner-1", "owner");

    private static readonly DateTime _oldest = new(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _midnight = new(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _middle = new(2026, 9, 2, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _newest = new(2026, 9, 3, 10, 0, 0, DateTimeKind.Utc);

    private static readonly Permission[] _allPermissions =
    [
        ContactCenterPermissions.ViewReports,
        ContactCenterPermissions.ListenToAllCallRecordings,
        ContactCenterPermissions.AccessSharedVoicemail,
    ];

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"cc-report-source-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_databasePath};Pooling=False"));
        _store.RegisterIndexes([new InteractionIndexProvider(), new SharedVoicemailIndexProvider()]);

        await _store.InitializeAsync(cancellationToken);
        await _store.InitializeCollectionAsync(ContactCenterStorage.CollectionName, cancellationToken);

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(cancellationToken);

            await InteractionQueryPlanFixture.MigrateAsync(_store, transaction);
            await new SharedVoicemailIndexMigrations { SchemaBuilder = new SchemaBuilder(_store.Configuration, transaction) }.CreateAsync();
            await transaction.CommitAsync(cancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            // Saved out of date order, so reading newest first is the store's doing, not the insertion order's.
            await session.SaveAsync(NewInteraction("interaction-middle", _middle), collection: ContactCenterStorage.CollectionName, cancellationToken: cancellationToken);
            await session.SaveAsync(DialedInteraction(), collection: ContactCenterStorage.CollectionName, cancellationToken: cancellationToken);
            await session.SaveAsync(NewInteraction("interaction-newest", _newest), collection: ContactCenterStorage.CollectionName, cancellationToken: cancellationToken);
            await session.SaveAsync(NewInteraction("interaction-midnight", _midnight), collection: ContactCenterStorage.CollectionName, cancellationToken: cancellationToken);

            await session.SaveAsync(Voicemail("voicemail-entitled", "queue-entitled"), collection: ContactCenterStorage.CollectionName, cancellationToken: cancellationToken);
            await session.SaveAsync(Voicemail("voicemail-other-team", "queue-other-team"), collection: ContactCenterStorage.CollectionName, cancellationToken: cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task WithoutViewReports_NothingIsListed_AndNoSchemaOrRowsAreReturned()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session, allowedPermissions: []);
        var context = new ReportDataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync(ContactCenterReportDataSets.Interactions, context, TestContext.Current.CancellationToken);
        var table = await source.QueryAsync(Query(ContactCenterReportDataSets.Interactions), TestContext.Current.CancellationToken);
        var withoutUser = await source.GetDataSetsAsync(new ReportDataSourceContext(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(schema);
        Assert.Empty(table.Rows);
        Assert.Empty(withoutUser);
    }

    [Fact]
    public async Task WithViewReportsOnly_RecordingsAndSharedVoicemail_AreNotListed()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session, allowedPermissions: [ContactCenterPermissions.ViewReports]);
        var context = new ReportDataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var recordings = await source.GetSchemaAsync(ContactCenterReportDataSets.CallRecordings, context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [ContactCenterReportDataSets.Interactions, ContactCenterReportDataSets.CallSessions],
            dataSets.Select(dataSet => dataSet.Name));
        Assert.Null(recordings);
    }

    [Fact]
    public async Task Interactions_Schema_FlagsIdentifiers_AndReferencesAgentsQueuesActivitiesAndUsers()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);

        // Act
        var schema = await source.GetSchemaAsync(ContactCenterReportDataSets.Interactions, new ReportDataSourceContext { User = _user }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(schema.FindField(ContactCenterReportDataSets.ItemIdField).IsIdentifier);
        AssertReference(schema.FindField(nameof(Interaction.AgentId)), ContactCenterReportDataSets.Source, ContactCenterReportDataSets.AgentProfiles, ContactCenterReportDataSets.ItemIdField);
        AssertReference(schema.FindField(nameof(Interaction.QueueId)), ContactCenterReportDataSets.Source, ContactCenterReportDataSets.Queues, ContactCenterReportDataSets.ItemIdField);
        AssertReference(schema.FindField(nameof(Interaction.ActivityItemId)), "Omnichannel", "Activities", ContactCenterReportDataSets.ItemIdField);
        AssertReference(schema.FindField(nameof(Interaction.CreatedById)), ReportsConstants.UsersDataSource, ReportsConstants.UsersDataSet, ReportsConstants.UserIdField);
        Assert.Equal(ReportDataType.DateTime, schema.FindField(nameof(Interaction.CreatedUtc)).DataType);
        Assert.Equal(ReportDataType.Decimal, schema.FindField("TalkSeconds").DataType);
        Assert.Equal(ReportDataType.Integer, schema.FindField("Dialer.AttemptNumber").DataType);
        Assert.Contains(schema.DataSet.References, reference => reference.DataSet == ContactCenterReportDataSets.AgentProfiles);
        Assert.DoesNotContain(schema.Fields, field => field.Name is nameof(Interaction.TechnicalMetadata) or nameof(Interaction.HandoffSummary) or nameof(Interaction.RecordingReference));
    }

    [Fact]
    public async Task Interactions_Rows_CarryUtcDates_EnumsAsText_DerivedTimes_AndDialerMetadata()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var query = Query(
            ContactCenterReportDataSets.Interactions,
            ContactCenterReportDataSets.ItemIdField,
            nameof(Interaction.Channel),
            nameof(Interaction.Direction),
            nameof(Interaction.Status),
            nameof(Interaction.AgentId),
            nameof(Interaction.QueueId),
            nameof(Interaction.CreatedUtc),
            nameof(Interaction.AnsweredUtc),
            "WaitSeconds",
            "TalkSeconds",
            "WrapUpSeconds",
            "Dialer.ProfileId",
            "Dialer.AttemptNumber",
            "Dialer.Outcome",
            "Dialer.AnswerClassification",
            "Dialer.AgentJoinedUtc");

        // Act
        var table = await source.QueryAsync(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(16, table.Fields.Count);

        var rows = Rows(table);
        var dialed = Assert.Single(rows, row => (string)row[ContactCenterReportDataSets.ItemIdField] == "interaction-dialed");
        Assert.Equal("Voice", dialed[nameof(Interaction.Channel)]);
        Assert.Equal("Outbound", dialed[nameof(Interaction.Direction)]);
        Assert.Equal("Ended", dialed[nameof(Interaction.Status)]);
        Assert.Equal("agent-1", dialed[nameof(Interaction.AgentId)]);
        Assert.Equal("queue-1", dialed[nameof(Interaction.QueueId)]);

        var created = Assert.IsType<DateTime>(dialed[nameof(Interaction.CreatedUtc)]);
        Assert.Equal(DateTimeKind.Utc, created.Kind);
        Assert.Equal(_oldest, created);
        Assert.Equal(DateTimeKind.Utc, Assert.IsType<DateTime>(dialed[nameof(Interaction.AnsweredUtc)]).Kind);

        Assert.Equal(30m, dialed["WaitSeconds"]);
        Assert.Equal(120m, dialed["TalkSeconds"]);
        Assert.Null(dialed["WrapUpSeconds"]);

        Assert.Equal("dialer-profile-1", dialed["Dialer.ProfileId"]);
        Assert.Equal(2L, dialed["Dialer.AttemptNumber"]);
        Assert.Equal("Connected", dialed["Dialer.Outcome"]);
        Assert.Equal("Human", dialed["Dialer.AnswerClassification"]);
        Assert.Equal(_oldest.AddSeconds(31), dialed["Dialer.AgentJoinedUtc"]);

        var plain = Assert.Single(rows, row => (string)row[ContactCenterReportDataSets.ItemIdField] == "interaction-middle");
        Assert.Null(plain["Dialer.ProfileId"]);
        Assert.Null(plain["Dialer.AttemptNumber"]);
        Assert.Null(plain["WaitSeconds"]);
    }

    [Fact]
    public async Task Interactions_NoConditions_ReadNewestFirst_AndTruncateAtMaxRows()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var query = Query(ContactCenterReportDataSets.Interactions, ContactCenterReportDataSets.ItemIdField);
        query.MaxRows = 2;

        // Act
        var table = await source.QueryAsync(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["interaction-newest", "interaction-middle"], Rows(table).Select(row => row[ContactCenterReportDataSets.ItemIdField]));
        Assert.True(table.Truncated);
    }

    [Theory]
    [InlineData(ReportFilterOperator.GreaterThanOrEqual)]
    [InlineData(ReportFilterOperator.GreaterThan)]
    public async Task Interactions_LowerBound_KeepsTheRowOnTheBound_AndLeavesOutOlderRows(ReportFilterOperator filterOperator)
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var query = Query(ContactCenterReportDataSets.Interactions, ContactCenterReportDataSets.ItemIdField);
        query.Conditions.Add(new ReportDataCondition { Field = nameof(Interaction.CreatedUtc), Operator = filterOperator, Values = [_midnight] });

        // Act
        var table = await source.QueryAsync(query, TestContext.Current.CancellationToken);

        // Assert
        // A strict bound is pushed down as an inclusive one: the report drops the row on the bound itself, the store
        // must not.
        Assert.Equal(
            ["interaction-newest", "interaction-middle", "interaction-midnight"],
            Rows(table).Select(row => row[ContactCenterReportDataSets.ItemIdField]));
        Assert.False(table.Truncated);
    }

    [Fact]
    public async Task Interactions_RangeAndUpperBound_NeverDropARowTheConditionsKeep()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var between = Query(ContactCenterReportDataSets.Interactions, ContactCenterReportDataSets.ItemIdField);
        between.Conditions.Add(new ReportDataCondition { Field = nameof(Interaction.CreatedUtc), Operator = ReportFilterOperator.Between, Values = [_midnight, _middle] });
        var before = Query(ContactCenterReportDataSets.Interactions, ContactCenterReportDataSets.ItemIdField);
        before.Conditions.Add(new ReportDataCondition { Field = nameof(Interaction.CreatedUtc), Operator = ReportFilterOperator.LessThan, Values = [_middle] });
        var otherField = Query(ContactCenterReportDataSets.Interactions, ContactCenterReportDataSets.ItemIdField);
        otherField.Conditions.Add(new ReportDataCondition { Field = nameof(Interaction.EndedUtc), Operator = ReportFilterOperator.GreaterThan, Values = [_newest.AddYears(1)] });

        // Act
        var betweenRows = Rows(await source.QueryAsync(between, TestContext.Current.CancellationToken));
        var beforeRows = Rows(await source.QueryAsync(before, TestContext.Current.CancellationToken));
        var otherFieldRows = Rows(await source.QueryAsync(otherField, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["interaction-middle", "interaction-midnight"], betweenRows.Select(row => row[ContactCenterReportDataSets.ItemIdField]));
        Assert.Equal(["interaction-middle", "interaction-midnight", "interaction-dialed"], beforeRows.Select(row => row[ContactCenterReportDataSets.ItemIdField]));

        // A condition on any other field is left to the report, so every interaction is read.
        Assert.Equal(4, otherFieldRows.Count);
    }

    [Fact]
    public async Task SharedVoicemails_ReadOnlyTheQueuesThePrincipalMayAnswer()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(
            session,
            [ContactCenterPermissions.ViewReports, ContactCenterPermissions.AccessSharedVoicemail],
            new SharedVoicemailAccess { CanAccess = true, QueueIds = ["queue-entitled"] });

        // Act
        var dataSets = await source.GetDataSetsAsync(new ReportDataSourceContext { User = _user }, TestContext.Current.CancellationToken);
        var table = await source.QueryAsync(Query(ContactCenterReportDataSets.SharedVoicemails, ContactCenterReportDataSets.ItemIdField, nameof(SharedVoicemail.QueueId)), TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(dataSets, dataSet => dataSet.Name == ContactCenterReportDataSets.SharedVoicemails);
        var row = Assert.Single(Rows(table));
        Assert.Equal("voicemail-entitled", row[ContactCenterReportDataSets.ItemIdField]);
    }

    [Fact]
    public async Task Registration_ListsOnlyTheDataSetsOfTheEnabledFeatures()
    {
        // Arrange
        // The base feature and Call Recording are on; Work Distribution, the dialer, agents and inbound voice are off,
        // so their startups never run.
        var services = new ServiceCollection();
        services.AddScoped(_ => Mock.Of<ISession>());
        services.AddSingleton(Authorization(_allPermissions));
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(ContentReportTestLocalizer<>));
        new ReportBuilderStartup().ConfigureServices(services);
        new RecordingReportBuilderStartup().ConfigureServices(services);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var source = Assert.Single(scope.ServiceProvider.GetServices<IReportDataSource>());

        // Act
        var dataSets = await source.GetDataSetsAsync(new ReportDataSourceContext { User = _user }, TestContext.Current.CancellationToken);
        var queues = await source.GetSchemaAsync(ContactCenterReportDataSets.Queues, new ReportDataSourceContext { User = _user }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ContactCenterReportDataSets.Source, source.Name);
        Assert.Equal(
            [
                ContactCenterReportDataSets.Interactions,
                ContactCenterReportDataSets.InteractionEvents,
                ContactCenterReportDataSets.CallSessions,
                ContactCenterReportDataSets.CallQuality,
                ContactCenterReportDataSets.CallRecordings,
            ],
            dataSets.Select(dataSet => dataSet.Name));
        Assert.Null(queues);
    }

    [Theory]
    [InlineData(typeof(ReportBuilderStartup), null)]
    [InlineData(typeof(QueuesReportBuilderStartup), ContactCenterConstants.Feature.Queues)]
    [InlineData(typeof(DialerReportBuilderStartup), ContactCenterConstants.Feature.Dialer)]
    [InlineData(typeof(RecordingReportBuilderStartup), ContactCenterConstants.Feature.Recording)]
    [InlineData(typeof(AgentServicesReportBuilderStartup), ContactCenterConstants.Feature.AgentServices)]
    [InlineData(typeof(AgentsReportBuilderStartup), ContactCenterConstants.Feature.Agents)]
    [InlineData(typeof(InboundVoiceReportBuilderStartup), ContactCenterConstants.Feature.InboundVoice)]
    public void Startups_BelongToTheFeatureThatStoresTheirRecords_AndRequireTheReportBuilder(Type startupType, string featureId)
    {
        // Arrange
        // A startup without a feature attribute belongs to the module's base feature.

        // Act
        var feature = startupType.GetCustomAttribute<FeatureAttribute>();
        var required = startupType.GetCustomAttribute<RequireFeaturesAttribute>();

        // Assert
        Assert.Equal(featureId, feature?.FeatureName);
        Assert.NotNull(required);
        Assert.Equal([ReportsConstants.BuilderFeature], required.RequiredFeatureNames);
    }

    [Fact]
    public void Startups_RegisterEveryDataSetOnce_WithTheDocumentedNames()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        new ReportBuilderStartup().ConfigureServices(services);
        new QueuesReportBuilderStartup().ConfigureServices(services);
        new DialerReportBuilderStartup().ConfigureServices(services);
        new RecordingReportBuilderStartup().ConfigureServices(services);
        new AgentServicesReportBuilderStartup().ConfigureServices(services);
        new AgentsReportBuilderStartup().ConfigureServices(services);
        new InboundVoiceReportBuilderStartup().ConfigureServices(services);

        // Assert
        var dataSetTypes = services
            .Where(descriptor => descriptor.ServiceType == typeof(IContactCenterReportDataSet))
            .Select(descriptor => descriptor.ImplementationType)
            .ToList();

        Assert.Equal(dataSetTypes.Count, dataSetTypes.Distinct().Count());
        Assert.Equal(
            [
                typeof(InteractionReportDataSet),
                typeof(InteractionEventReportDataSet),
                typeof(CallSessionReportDataSet),
                typeof(CallQualityReportDataSet),
                typeof(QueueReportDataSet),
                typeof(QueueGroupReportDataSet),
                typeof(QueueItemReportDataSet),
                typeof(DialerProfileReportDataSet),
                typeof(CallbackRequestReportDataSet),
                typeof(CallRecordingReportDataSet),
                typeof(AgentProfileReportDataSet),
                typeof(AgentSessionReportDataSet),
                typeof(SharedVoicemailReportDataSet),
            ],
            dataSetTypes);
    }

    private static ContactCenterReportDataSource Source(ISession session, Permission[] allowedPermissions = null, SharedVoicemailAccess voicemailAccess = null)
    {
        var authorizationService = Authorization(allowedPermissions ?? _allPermissions);
        var voicemailAuthorization = new Mock<ISharedVoicemailAuthorizationService>();

        voicemailAuthorization
            .Setup(service => service.GetAccessAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(voicemailAccess ?? SharedVoicemailAccess.None);

        IContactCenterReportDataSet[] dataSets =
        [
            new InteractionReportDataSet(session, authorizationService, new ContentReportTestLocalizer<InteractionReportDataSet>()),
            new CallRecordingReportDataSet(session, authorizationService, new ContentReportTestLocalizer<CallRecordingReportDataSet>()),
            new CallSessionReportDataSet(session, authorizationService, new ContentReportTestLocalizer<CallSessionReportDataSet>()),
            new SharedVoicemailReportDataSet(session, authorizationService, voicemailAuthorization.Object, new ContentReportTestLocalizer<SharedVoicemailReportDataSet>()),
        ];

        return new ContactCenterReportDataSource(dataSets, new ContentReportTestLocalizer<ContactCenterReportDataSource>());
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

    private static Interaction NewInteraction(string itemId, DateTime createdUtc)
    {
        return new Interaction
        {
            ItemId = itemId,
            Channel = InteractionChannel.Voice,
            Direction = InteractionDirection.Inbound,
            ProviderName = "Provider",
            CreatedUtc = createdUtc,
            ModifiedUtc = createdUtc,
        }.RestorePersistedStatus(InteractionStatus.Connected);
    }

    private static Interaction DialedInteraction()
    {
        var interaction = new Interaction
        {
            ItemId = "interaction-dialed",
            Channel = InteractionChannel.Voice,
            Direction = InteractionDirection.Outbound,
            ActivityItemId = "activity-1",
            ProviderName = "Provider",
            QueueId = "queue-1",
            AgentId = "agent-1",
            CreatedUtc = _oldest,
            StartedUtc = _oldest,
            AnsweredUtc = _oldest.AddSeconds(30),
            EndedUtc = _oldest.AddSeconds(150),
            ModifiedUtc = _oldest.AddSeconds(150),
        }.RestorePersistedStatus(InteractionStatus.Ended);

        DialerCallMetadata.StampDial(interaction, new DialerProfile { ItemId = "dialer-profile-1", MaxAttempts = 3 }, 2);
        DialerCallMetadata.MarkAgentJoined(interaction, _oldest.AddSeconds(31));
        DialerCallMetadata.SetOutcome(interaction, "Connected");
        interaction.TechnicalMetadata[ContactCenterConstants.TelephonyMetadata.AnswerClassification] = "Human";

        return interaction;
    }

    private static SharedVoicemail Voicemail(string itemId, string queueId)
    {
        return new SharedVoicemail
        {
            ItemId = itemId,
            QueueId = queueId,
            InteractionId = "interaction-middle",
            ReceivedUtc = _middle,
            CreatedUtc = _middle,
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

    private static void AssertReference(ReportFieldDescriptor field, string source, string dataSet, string fieldName)
    {
        Assert.True(field.IsIdentifier);

        var reference = Assert.Single(field.References);
        Assert.Equal(source, reference.Source);
        Assert.Equal(dataSet, reference.DataSet);
        Assert.Equal(fieldName, reference.Field);
    }

    private static List<Dictionary<string, object>> Rows(ReportDataTable table)
    {
        return table.Rows
            .Select(row => table.Fields.Select((field, index) => (field.Name, Value: row[index])).ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal))
            .ToList();
    }
}

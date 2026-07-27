using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Establishes that routing state has one owner. Assignment, reservation, and attempt counters used to live on
/// the CRM <see cref="OmnichannelActivity"/> document, which meant the reservation loop had to write the same
/// document a person could be editing in the admin UI at the same moment. Two writers on one optimistic-concurrency
/// document produce a lost update or a thrown <see cref="ConcurrencyException"/> at the routing commit, and losing
/// a reservation commit strands live work. Routing now owns <see cref="ContactCenterWorkState"/>, and the activity
/// keeps the same fields only as a read model that is reconciled after the routing transaction has committed.
/// </summary>
/// <remarks>
/// The read model was retained rather than deleted because it is load-bearing outside routing:
/// <c>OmnichannelActivityAuthorizationHandler</c> decides ownership from <c>AssignedToId</c>, and the CRM admin
/// list, filters, and reports read the same columns through <c>OmnichannelActivityIndex</c>. Deleting them would
/// either remove CRM function or invert the layering by making the CRM query a Contact Center store. The
/// consequence is that the read model can lag, so the authority test below pins which of the two a routing
/// decision is required to read.
/// </remarks>
public sealed class ContactCenterWorkStateAuthorityTests
{
    private static readonly DateTime _now = new(2026, 8, 3, 9, 0, 0, DateTimeKind.Utc);

    private static readonly string[] _workStateMemberNames =
    [
        nameof(OmnichannelActivity.AssignmentStatus),
        nameof(OmnichannelActivity.ReservationId),
        nameof(OmnichannelActivity.ReservedById),
        nameof(OmnichannelActivity.ReservedByUsername),
        nameof(OmnichannelActivity.ReservedUtc),
        nameof(OmnichannelActivity.ReservationExpiresUtc),
        nameof(OmnichannelActivity.AssignedToId),
        nameof(OmnichannelActivity.AssignedToUsername),
        nameof(OmnichannelActivity.AssignedToUtc),
        nameof(OmnichannelActivity.Attempts),
    ];

    private static readonly string[] _sourceProjectFolders =
    [
        Path.Combine("Core", "CrestApps.OrchardCore.ContactCenter.Core"),
        Path.Combine("Modules", "CrestApps.OrchardCore.ContactCenter"),
        Path.Combine("Modules", "CrestApps.OrchardCore.Telephony"),
        Path.Combine("Modules", "CrestApps.OrchardCore.Asterisk"),
        Path.Combine("Modules", "CrestApps.OrchardCore.DialPad"),
    ];

    /// <summary>
    /// The only file allowed to copy routing state onto the CRM activity. Everything else has to go through it,
    /// so that "what the read model contains" has exactly one definition and cannot drift field by field.
    /// </summary>
    private const string ProjectorFileName = "ContactCenterWorkStateProjector.cs";

    /// <summary>
    /// The <see cref="IContactCenterActivityWriter"/> members whose callback receives the CRM activity.
    /// </summary>
    private static readonly string[] _activityWriterMethodNames = ["ScheduleUpdateAsync", "UpdateAsync"];

    [Fact]
    public void NoContactCenterSource_WritesRoutingStateOntoTheCrmActivity_OutsideTheProjector()
    {
        // Arrange
        var violations = new List<string>();

        // Act
        foreach (var file in EnumerateContactCenterSources())
        {
            if (string.Equals(Path.GetFileName(file), ProjectorFileName, StringComparison.Ordinal))
            {
                continue;
            }

            violations.AddRange(FindRoutingStateWritesOnActivities(file));
        }

        // Assert
        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void TheAuthorityScan_ReportsEveryRoutingStateWrite_InTheProjectorItself()
    {
        // Arrange
        // The scan above only fails when it recognizes a receiver as a CRM activity, so a change that makes it
        // stop recognizing them would silently pass everything. The projector is the known-positive control:
        // it writes all ten fields on an activity, and the scan has to see all ten.
        var projector = Directory
            .EnumerateFiles(Path.Combine(FindRepositoryRoot(), "src"), ProjectorFileName, SearchOption.AllDirectories)
            .Single(file => !IsGeneratedPath(file));

        // Act
        var detected = FindRoutingStateWritesOnActivities(projector);

        // Assert
        Assert.Equal(_workStateMemberNames.Length, detected.Count);
    }

    /// <summary>
    /// Reports every assignment in a file that writes a routing-owned field onto a CRM activity.
    /// </summary>
    /// <param name="file">The full path of the source file to scan.</param>
    /// <returns>One description per write, empty when the file writes none.</returns>
    private static List<string> FindRoutingStateWritesOnActivities(string file)
    {
        var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot();
        var activityIdentifiers = CollectActivityIdentifiers(root);
        var writes = new List<string>();

        foreach (var assignment in root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (assignment.Left is not MemberAccessExpressionSyntax memberAccess ||
                !_workStateMemberNames.Contains(memberAccess.Name.Identifier.ValueText, StringComparer.Ordinal) ||
                memberAccess.Expression is not IdentifierNameSyntax receiver ||
                !activityIdentifiers.Contains(receiver.Identifier.ValueText))
            {
                continue;
            }

            writes.Add(
                $"{Path.GetFileName(file)}:{LineOf(assignment)} writes {memberAccess.Name.Identifier.ValueText} " +
                $"onto the CRM activity '{receiver.Identifier.ValueText}'.");
        }

        foreach (var creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
        {
            if (creation.Type is not IdentifierNameSyntax createdType ||
                !string.Equals(createdType.Identifier.ValueText, nameof(OmnichannelActivity), StringComparison.Ordinal) ||
                creation.Initializer is null)
            {
                continue;
            }

            foreach (var initializer in creation.Initializer.Expressions.OfType<AssignmentExpressionSyntax>())
            {
                if (initializer.Left is IdentifierNameSyntax member &&
                    _workStateMemberNames.Contains(member.Identifier.ValueText, StringComparer.Ordinal))
                {
                    writes.Add(
                        $"{Path.GetFileName(file)}:{LineOf(initializer)} initializes {member.Identifier.ValueText} " +
                        $"on a new {nameof(OmnichannelActivity)}.");
                }
            }
        }

        return writes;
    }


    [Fact]
    public void TheProjector_IsPresentAndCopiesEveryRoutingOwnedField()
    {
        // Arrange
        var projectorFiles = Directory
            .EnumerateFiles(Path.Combine(FindRepositoryRoot(), "src"), ProjectorFileName, SearchOption.AllDirectories)
            .Where(file => !IsGeneratedPath(file));

        // Act
        var source = File.ReadAllText(Assert.Single(projectorFiles));

        // Assert
        foreach (var member in _workStateMemberNames)
        {
            Assert.Contains($"activity.{member} = workState.{member};", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task GetAsync_WhenTheCrmReadModelIsStale_ReturnsTheRoutingOwnedState()
    {
        // Arrange
        var activity = new OmnichannelActivity
        {
            ItemId = "activity-1",
            AssignmentStatus = ActivityAssignmentStatus.Available,
            AssignedToId = null,
            Attempts = 1,
        };

        var workState = new ContactCenterWorkState
        {
            ItemId = "work-state-1",
            ActivityItemId = "activity-1",
            AssignmentStatus = ActivityAssignmentStatus.Assigned,
            AssignedToId = "agent-9",
            Attempts = 4,
        };

        var service = CreateWorkStateService(activity, workState, out _);

        // Act
        var resolved = await service.GetAsync("activity-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ActivityAssignmentStatus.Assigned, resolved.AssignmentStatus);
        Assert.Equal("agent-9", resolved.AssignedToId);
        Assert.Equal(4, resolved.Attempts);
        Assert.True(
            ContactCenterWorkStateProjector.HasDivergence(activity, resolved),
            "The arrangement must keep the read model stale, otherwise this proves nothing about which one was read.");
    }

    [Fact]
    public async Task GetAsync_WhenWorkInFlightPredatesTheWorkStateDocument_AdoptsTheReadModel()
    {
        // Arrange
        var activity = new OmnichannelActivity
        {
            ItemId = "activity-1",
            AssignmentStatus = ActivityAssignmentStatus.Assigned,
            AssignedToId = "agent-3",
            Attempts = 6,
        };

        var service = CreateWorkStateService(activity, workState: null, out _);

        // Act
        var resolved = await service.GetAsync("activity-1", TestContext.Current.CancellationToken);

        // Assert
        // An upgraded installation has live work with no work state document. Reporting it as unassigned with
        // zero attempts would re-offer work an agent already holds and reset the dialer's attempt cap.
        Assert.Equal(ActivityAssignmentStatus.Assigned, resolved.AssignmentStatus);
        Assert.Equal("agent-3", resolved.AssignedToId);
        Assert.Equal(6, resolved.Attempts);
    }

    [Fact]
    public async Task MutateAsync_DoesNotWriteTheCrmActivity_InsideTheRoutingTransaction()
    {
        // Arrange
        var activity = new OmnichannelActivity
        {
            ItemId = "activity-1",
        };

        var service = CreateWorkStateService(activity, workState: null, out var scopeExecutor);
        scopeExecutor.ScheduleAfterCommitResult = true;

        // Act
        await service.MutateAsync(
            "activity-1",
            state => state.AssignmentStatus = ActivityAssignmentStatus.Reserved,
            TestContext.Current.CancellationToken);

        // Assert
        // The reconciliation is queued for after the routing transaction commits, so the activity is untouched
        // while the routing session is still open.
        Assert.Equal(ActivityAssignmentStatus.Unassigned, activity.AssignmentStatus);
        Assert.NotNull(scopeExecutor.ScheduledOperation);
    }

    [Fact]
    public async Task ReserveAsync_WhenTheCrmEditsTheSameActivity_NeitherWriterConflicts()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"contact-center-work-state-{Guid.NewGuid():N}.db");
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes(
        [
            new QueueItemIndexProvider(),
            new AgentProfileIndexProvider(),
            new ActivityReservationIndexProvider(),
            new ContactCenterWorkStateIndexProvider(),
        ]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(ContactCenterConstants.CollectionName, TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(OmnichannelConstants.CollectionName, TestContext.Current.CancellationToken);
        await CreateIndexSchemaAsync(store);

        try
        {
            var seed = await SeedAsync(store);

            // The CRM reads the activity before routing runs and writes it afterwards, which is the interleaving
            // that used to lose one of the two writes.
            await using var crmSession = store.CreateSession();
            var crmActivity = await LoadActivityAsync(crmSession, "activity-1");

            await using var routingSession = store.CreateSession();
            await using var routingProvider = CreateRoutingServiceProvider(routingSession, seed);

            // Act
            var reservation = await routingProvider
                .GetRequiredService<IActivityReservationService>()
                .ReserveAsync(seed.QueueItem, seed.Agent, 30, TestContext.Current.CancellationToken);

            await routingSession.SaveChangesAsync(TestContext.Current.CancellationToken);

            crmActivity.Notes = "Customer asked for a callback.";
            await crmSession.SaveAsync(
                crmActivity,
                checkConcurrency: true,
                collection: OmnichannelConstants.CollectionName,
                cancellationToken: TestContext.Current.CancellationToken);
            var crmException = await Record.ExceptionAsync(
                () => crmSession.SaveChangesAsync(TestContext.Current.CancellationToken));

            // Assert
            Assert.Null(crmException);
            Assert.NotNull(reservation);

            await using var verificationSession = store.CreateSession();
            var persistedActivity = await LoadActivityAsync(verificationSession, "activity-1");
            var persistedWorkState = await verificationSession
                .Query<ContactCenterWorkState, ContactCenterWorkStateIndex>(
                    index => index.ActivityItemId == "activity-1",
                    collection: ContactCenterConstants.CollectionName)
                .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

            Assert.Equal("Customer asked for a callback.", persistedActivity.Notes);
            Assert.NotNull(persistedWorkState);
            Assert.Equal(ActivityAssignmentStatus.Reserved, persistedWorkState.AssignmentStatus);
            Assert.Equal(reservation.ItemId, persistedWorkState.ReservationId);
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    private static ContactCenterWorkStateService CreateWorkStateService(
        OmnichannelActivity activity,
        ContactCenterWorkState workState,
        out TestContactCenterScopeExecutor scopeExecutor)
    {
        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager
            .Setup(manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string itemId, CancellationToken _) =>
                string.Equals(itemId, activity?.ItemId, StringComparison.Ordinal) ? activity : null);

        var workStateManager = new Mock<IContactCenterWorkStateManager>();
        workStateManager
            .Setup(manager => manager.FindByActivityIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workState);
        workStateManager
            .Setup(manager => manager.NewAsync(It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactCenterWorkState { ItemId = "work-state-new" });

        var clock = new Mock<IClock>();
        clock.SetupGet(service => service.UtcNow).Returns(_now);

        scopeExecutor = new TestContactCenterScopeExecutor(new ServiceCollection().BuildServiceProvider());

        var projection = new ContactCenterWorkStateActivityProjection(
            workStateManager.Object,
            activityManager.Object,
            scopeExecutor,
            NullLogger<ContactCenterWorkStateActivityProjection>.Instance);

        return new ContactCenterWorkStateService(
            workStateManager.Object,
            [projection],
            scopeExecutor,
            clock.Object);
    }

    private static ServiceProvider CreateRoutingServiceProvider(
        ISession session,
        (QueueItem QueueItem, AgentProfile Agent) seed)
    {
        var queueItemManager = new QueueItemManager(
            new QueueItemStore(session),
            [],
            NullLogger<CatalogManager<QueueItem>>.Instance);
        var agentManager = new AgentProfileManager(
            new AgentProfileStore(session),
            [],
            NullLogger<CatalogManager<AgentProfile>>.Instance);
        var reservationManager = new ActivityReservationManager(
            new ActivityReservationStore(session),
            [],
            NullLogger<CatalogManager<ActivityReservation>>.Instance);

        var availabilityService = new Mock<IAgentAvailabilityService>();
        availabilityService
            .Setup(service => service.GetAsync(seed.Agent.ItemId, seed.QueueItem.QueueId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentAvailability
            {
                Agent = seed.Agent,
            });

        // The activity manager is bound to the routing session on purpose: if routing were still writing the
        // activity, that write would land in the routing transaction and bump the document version the CRM holds.
        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager
            .Setup(manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((itemId, _) => new ValueTask<OmnichannelActivity>(LoadActivityAsync(session, itemId)));
        activityManager
            .Setup(manager => manager.UpdateAsync(
                It.IsAny<OmnichannelActivity>(),
                It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                It.IsAny<CancellationToken>()))
            .Returns<OmnichannelActivity, System.Text.Json.Nodes.JsonNode, CancellationToken>(
                (activity, _, cancellationToken) => new ValueTask(session.SaveAsync(
                    activity,
                    checkConcurrency: true,
                    collection: OmnichannelConstants.CollectionName,
                    cancellationToken: cancellationToken)));

        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(service => service.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync((null, true));

        var clock = new Mock<IClock>();
        clock.SetupGet(service => service.UtcNow).Returns(_now);

        var services = new ServiceCollection();
        services.AddSingleton(reservationManager);
        services.AddSingleton<IActivityReservationManager>(reservationManager);
        services.AddSingleton(queueItemManager);
        services.AddSingleton<IQueueItemManager>(queueItemManager);
        services.AddSingleton(agentManager);
        services.AddSingleton<IAgentProfileManager>(agentManager);
        services.AddSingleton(Mock.Of<IActivityQueueManager>());
        services.AddSingleton(Mock.Of<IActivityQueueService>());
        services.AddSingleton(Mock.Of<IInteractionManager>());
        services.AddSingleton(activityManager.Object);
        services.AddSingleton(availabilityService.Object);
        services.AddSingleton(Mock.Of<IContactCenterEventPublisher>());
        // Production has a shell scope, so the CRM reconciliation is deferred until after the routing
        // transaction commits. Without that, the fallback would join the routing session and the test would be
        // measuring the fallback rather than the shipped behaviour.
        services.AddSingleton<IContactCenterScopeExecutor>(
            new TestContactCenterScopeExecutor(new ServiceCollection().BuildServiceProvider())
            {
                ScheduleAfterCommitResult = true,
            });
        services.AddSingleton<IEnumerable<ITelephonyService>>([]);
        services.AddSingleton(distributedLock.Object);
        services.AddSingleton(session);
        services.AddSingleton(clock.Object);
        services.AddLogging();
        services.AddSingleton<IContactCenterWorkStateStore>(new ContactCenterWorkStateStore(session));
        services.AddSingleton<IContactCenterWorkStateManager>(provider => new ContactCenterWorkStateManager(
            provider.GetRequiredService<IContactCenterWorkStateStore>(),
            [],
            NullLogger<CatalogManager<ContactCenterWorkState>>.Instance));
        services.AddSingleton<IContactCenterWorkStateActivityProjection, ContactCenterWorkStateActivityProjection>();
        services.AddSingleton<IContactCenterWorkStateService, ContactCenterWorkStateService>();
        services.AddSingleton<IContactCenterActivityWriter, ContactCenterActivityWriter>();
        services.AddSingleton<IActivityReservationService, ActivityReservationService>();

        return services.BuildServiceProvider();
    }

    private static async Task<OmnichannelActivity> LoadActivityAsync(ISession session, string activityItemId)
    {
        var activities = await session
            .Query<OmnichannelActivity>(collection: OmnichannelConstants.CollectionName)
            .ListAsync(TestContext.Current.CancellationToken);

        return activities.FirstOrDefault(activity =>
            string.Equals(activity.ItemId, activityItemId, StringComparison.Ordinal));
    }

    private static async Task<(QueueItem QueueItem, AgentProfile Agent)> SeedAsync(IStore store)
    {
        await using var session = store.CreateSession();
        var queueItemManager = new QueueItemManager(
            new QueueItemStore(session),
            [],
            NullLogger<CatalogManager<QueueItem>>.Instance);
        var agentManager = new AgentProfileManager(
            new AgentProfileStore(session),
            [],
            NullLogger<CatalogManager<AgentProfile>>.Instance);

        var queueItem = await queueItemManager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);
        queueItem.ItemId = "queue-item-1";
        queueItem.QueueId = "queue-1";
        queueItem.ActivityItemId = "activity-1";
        queueItem.Status = QueueItemStatus.Waiting;
        await queueItemManager.CreateAsync(queueItem, cancellationToken: TestContext.Current.CancellationToken);

        var agent = await agentManager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);
        agent.ItemId = "agent-1";
        agent.UserId = "user-1";
        agent.PresenceStatus = AgentPresenceStatus.Available;
        await agentManager.CreateAsync(agent, cancellationToken: TestContext.Current.CancellationToken);

        await session.SaveAsync(
            new OmnichannelActivity
            {
                ItemId = "activity-1",
            },
            collection: OmnichannelConstants.CollectionName,
            cancellationToken: TestContext.Current.CancellationToken);

        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (queueItem, agent);
    }

    private static async Task CreateIndexSchemaAsync(IStore store)
    {
        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

        await schemaBuilder.CreateMapIndexTableAsync<QueueItemIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("QueueId", column => column.WithLength(26))
            .Column<string>("ActivityItemId", column => column.WithLength(26))
            .Column<string>("ActivityClaimKey", column => column.NotNull().Unique().WithLength(26))
            .Column<string>("Status", column => column.WithLength(50))
            .Column<string>("Priority", column => column.WithLength(50))
            .Column<string>("AgentId", column => column.WithLength(26))
            .Column<DateTime>("EnqueuedUtc", column => column.NotNull()),
            collection: ContactCenterConstants.CollectionName);

        await schemaBuilder.CreateMapIndexTableAsync<AgentProfileIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(255))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<string>("PresenceStatus", column => column.WithLength(50)),
            collection: ContactCenterConstants.CollectionName);

        await schemaBuilder.CreateMapIndexTableAsync<ContactCenterWorkStateIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("ActivityItemId", column => column.NotNull().Unique().WithLength(26))
            .Column<string>("AssignmentStatus", column => column.WithLength(50))
            .Column<string>("ReservationId", column => column.WithLength(26))
            .Column<string>("ReservedById", column => column.WithLength(26))
            .Column<string>("AssignedToId", column => column.WithLength(26)),
            collection: ContactCenterConstants.CollectionName);

        await schemaBuilder.CreateMapIndexTableAsync<ActivityReservationIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("ActivityItemId", column => column.WithLength(26))
            .Column<string>("ActivityClaimKey", column => column.NotNull().Unique().WithLength(26))
            .Column<string>("AgentId", column => column.WithLength(26))
            .Column<string>("AgentClaimKey", column => column.NotNull().Unique().WithLength(26))
            .Column<string>("Status", column => column.WithLength(50))
            .Column<DateTime>("ExpiresUtc", column => column.NotNull()),
            collection: ContactCenterConstants.CollectionName);

        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Collects the identifiers in a file that hold a CRM <see cref="OmnichannelActivity"/>. The routing-owned
    /// member names are shared with other documents — a queue item and a provider command both carry a
    /// <c>ReservationId</c> they legitimately own — so the receiver has to be classified rather than the member.
    /// </summary>
    /// <param name="root">The parsed syntax root of the file being scanned.</param>
    /// <returns>The set of identifier names that hold a CRM activity.</returns>
    private static HashSet<string> CollectActivityIdentifiers(SyntaxNode root)
    {
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        var activityTypeName = nameof(OmnichannelActivity);

        foreach (var declaration in root.DescendantNodes().OfType<VariableDeclarationSyntax>())
        {
            var isActivityTyped = declaration.Type.ToString().Contains(activityTypeName, StringComparison.Ordinal);

            foreach (var variable in declaration.Variables)
            {
                if (isActivityTyped || IsActivityProducingInitializer(variable.Initializer?.Value))
                {
                    identifiers.Add(variable.Identifier.ValueText);
                }
            }
        }

        foreach (var parameter in root.DescendantNodes().OfType<ParameterSyntax>())
        {
            if (parameter.Type?.ToString().Contains(activityTypeName, StringComparison.Ordinal) == true)
            {
                identifiers.Add(parameter.Identifier.ValueText);
            }
        }

        foreach (var statement in root.DescendantNodes().OfType<ForEachStatementSyntax>())
        {
            if (statement.Type.ToString().Contains(activityTypeName, StringComparison.Ordinal))
            {
                identifiers.Add(statement.Identifier.ValueText);
            }
        }

        // The mutation callback handed to the activity writer has an inferred parameter type, so it is matched
        // by the invocation it belongs to rather than by a written type.
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax member ||
                !_activityWriterMethodNames.Contains(member.Name.Identifier.ValueText, StringComparer.Ordinal) ||
                member.Expression is not IdentifierNameSyntax writer ||
                !writer.Identifier.ValueText.Contains("activityWriter", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var argument in invocation.ArgumentList.Arguments)
            {
                switch (argument.Expression)
                {
                    case SimpleLambdaExpressionSyntax simpleLambda:
                        identifiers.Add(simpleLambda.Parameter.Identifier.ValueText);
                        break;

                    case ParenthesizedLambdaExpressionSyntax parenthesizedLambda:
                        foreach (var parameter in parenthesizedLambda.ParameterList.Parameters)
                        {
                            identifiers.Add(parameter.Identifier.ValueText);
                        }

                        break;
                }
            }
        }

        return identifiers;
    }

    /// <summary>
    /// Determines whether an initializer produces a CRM activity, covering the <c>var</c> locals that hold one.
    /// </summary>
    /// <param name="initializer">The initializer expression, or <see langword="null"/> when there is none.</param>
    /// <returns><see langword="true"/> when the initializer yields a CRM activity; otherwise, <see langword="false"/>.</returns>
    private static bool IsActivityProducingInitializer(ExpressionSyntax initializer)
    {
        var expression = initializer switch
        {
            AwaitExpressionSyntax awaited => awaited.Expression,
            _ => initializer,
        };

        return expression switch
        {
            ObjectCreationExpressionSyntax creation =>
                creation.Type.ToString().Contains(nameof(OmnichannelActivity), StringComparison.Ordinal),
            InvocationExpressionSyntax invocation =>
                invocation.Expression is MemberAccessExpressionSyntax member &&
                member.Expression is IdentifierNameSyntax receiver &&
                receiver.Identifier.ValueText.Contains("activityManager", StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }


    private static IEnumerable<string> EnumerateContactCenterSources()
    {
        var repositoryRoot = FindRepositoryRoot();

        foreach (var projectFolder in _sourceProjectFolders)
        {
            var root = Path.Combine(repositoryRoot, "src", projectFolder);

            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (IsGeneratedPath(file))
                {
                    continue;
                }

                yield return file;
            }
        }
    }

    private static bool IsGeneratedPath(string file)
    {
        var directory = Path.GetDirectoryName(file) ?? string.Empty;

        return directory.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            directory.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
    }

    private static int LineOf(SyntaxNode node)
        => node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ??
            throw new InvalidOperationException("The repository root could not be located.");
    }
}

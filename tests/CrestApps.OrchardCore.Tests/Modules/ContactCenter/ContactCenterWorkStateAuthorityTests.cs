using System.Runtime.InteropServices;
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
using CrestApps.OrchardCore.Tests.Utilities;

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

    /// <summary>
    /// The namespaces the SDK imports implicitly, which the scan's own compilation has to import as well.
    /// </summary>
    private static readonly string[] _implicitUsings =
    [
        "System",
        "System.Collections.Generic",
        "System.IO",
        "System.Linq",
        "System.Net.Http",
        "System.Threading",
        "System.Threading.Tasks",
    ];

    private static readonly string[] _scannedAssemblyNames =
    [
        "CrestApps.OrchardCore.ContactCenter.Abstractions",
        "CrestApps.OrchardCore.ContactCenter.Core",
        "CrestApps.OrchardCore.Telephony.Core",
        "CrestApps.OrchardCore.ContactCenter",
        "CrestApps.OrchardCore.Telephony",
        "CrestApps.OrchardCore.Asterisk",
        "CrestApps.OrchardCore.DialPad",
    ];

    private static readonly string[] _sourceProjectFolders =
    [
        Path.Combine("Abstractions", "CrestApps.OrchardCore.ContactCenter.Abstractions"),
        Path.Combine("Core", "CrestApps.OrchardCore.ContactCenter.Core"),
        Path.Combine("Core", "CrestApps.OrchardCore.Telephony.Core"),
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
    /// The scanned sources compiled once, so a receiver is classified by the type the compiler gives it rather
    /// than by the shape of the identifier that names it.
    /// </summary>
    private static readonly Lazy<ScannedSources> _scannedSources = new(CompileScannedSources, LazyThreadSafetyMode.ExecutionAndPublication);

    [Fact]
    public void NoContactCenterSource_WritesRoutingStateOntoTheCrmActivity_OutsideTheProjector()
    {
        // Arrange
        var violations = new List<RoutingStateWrite>();

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
        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations.Select(violation => violation.Description)));
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
        // Counting alone would be satisfied by ten unresolved reports, which is exactly the blinded scan this
        // control exists to catch, so each one has to have been recognized as the CRM activity.
        Assert.Equal(_workStateMemberNames.Length, detected.Count);
        Assert.All(detected, write => Assert.Equal(ReceiverKind.Activity, write.Kind));
    }

    [Theory]
    [InlineData("ActivityReservationService.cs")]
    [InlineData("ProviderCommandStateService.cs")]
    public void TheAuthorityScan_LeavesRoutingOwnedFieldsAlone_WhenTheyBelongToAnotherDocument(string fileName)
    {
        // Arrange
        // The known-negative control. These files write the same member names onto a queue item and a provider
        // command, which own them. A scan that reported them would be reporting the member rather than the
        // receiver, and the fail-closed rule would then have to be relaxed to keep the build green.
        var file = Directory
            .EnumerateFiles(Path.Combine(FindRepositoryRoot(), "src"), fileName, SearchOption.AllDirectories)
            .Single(candidate => !IsGeneratedPath(candidate));

        // Act
        var detected = FindRoutingStateWritesOnActivities(file);

        // Assert
        Assert.Empty(detected);
    }

    /// <summary>
    /// Reports every assignment in a file that writes a routing-owned field onto a CRM activity, and every
    /// assignment whose receiver the compiler could not resolve.
    /// </summary>
    /// <param name="file">The full path of the source file to scan.</param>
    /// <returns>One report per write, empty when the file writes none.</returns>
    private static List<RoutingStateWrite> FindRoutingStateWritesOnActivities(string file)
    {
        var sources = _scannedSources.Value;

        if (!sources.TreesByPath.TryGetValue(file, out var tree))
        {
            throw new InvalidOperationException(
                $"'{file}' is not one of the sources the authority scan compiled, so its receivers cannot be classified.");
        }

        var model = sources.Compilation.GetSemanticModel(tree);
        var root = tree.GetRoot();
        var writes = new List<RoutingStateWrite>();
        var fileName = Path.GetFileName(file);

        foreach (var node in root.DescendantNodes())
        {
            // Every form that stores into a member: simple and compound assignment, and increment or decrement,
            // which is how the one numeric routing field is written everywhere it is written legitimately.
            var written = node switch
            {
                AssignmentExpressionSyntax assignment => assignment.Left as MemberAccessExpressionSyntax,
                PrefixUnaryExpressionSyntax prefix when IsIncrementOrDecrement(prefix.Kind()) => prefix.Operand as MemberAccessExpressionSyntax,
                PostfixUnaryExpressionSyntax postfix when IsIncrementOrDecrement(postfix.Kind()) => postfix.Operand as MemberAccessExpressionSyntax,
                _ => null,
            };

            if (written is null || !_workStateMemberNames.Contains(written.Name.Identifier.ValueText, StringComparer.Ordinal))
            {
                continue;
            }

            var member = written.Name.Identifier.ValueText;
            var receiver = written.Expression.ToString();
            var kind = Classify(model, written.Expression);

            if (kind == ReceiverKind.Activity)
            {
                writes.Add(new RoutingStateWrite(
                    kind,
                    $"{fileName}:{LineOf(node)} writes {member} onto the CRM activity '{receiver}'."));
            }
            else if (kind == ReceiverKind.Unresolved)
            {
                writes.Add(new RoutingStateWrite(
                    kind,
                    $"{fileName}:{LineOf(node)} writes {member} onto '{receiver}', whose type the authority scan " +
                    $"could not resolve, so it cannot be shown not to be a CRM activity."));
            }
        }

        // Object initializers write the same fields without a receiver expression to classify, so the created
        // type is classified instead. Target-typed creations are included, because the type is only inferable.
        foreach (var creation in root.DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>())
        {
            if (creation.Initializer is null)
            {
                continue;
            }

            var kind = Classify(model, creation);

            if (kind == ReceiverKind.OtherDocument)
            {
                continue;
            }

            foreach (var initializer in creation.Initializer.Expressions.OfType<AssignmentExpressionSyntax>())
            {
                if (initializer.Left is not IdentifierNameSyntax member ||
                    !_workStateMemberNames.Contains(member.Identifier.ValueText, StringComparer.Ordinal))
                {
                    continue;
                }

                writes.Add(new RoutingStateWrite(
                    kind,
                    kind == ReceiverKind.Activity
                        ? $"{fileName}:{LineOf(initializer)} initializes {member.Identifier.ValueText} on a new {nameof(OmnichannelActivity)}."
                        : $"{fileName}:{LineOf(initializer)} initializes {member.Identifier.ValueText} on a created object whose " +
                            $"type the authority scan could not resolve, so it cannot be shown not to be a CRM activity."));
            }
        }

        return writes;
    }

    /// <summary>
    /// Classifies the receiver of a routing-owned member write.
    /// </summary>
    /// <param name="model">The semantic model of the file the expression belongs to.</param>
    /// <param name="expression">The receiver expression to classify.</param>
    /// <returns>
    /// The classification. A receiver the compiler cannot type is reported as
    /// <see cref="ReceiverKind.Unresolved"/> rather than assumed to be something other than a CRM activity.
    /// </returns>
    private static bool IsIncrementOrDecrement(SyntaxKind kind)
        => kind is SyntaxKind.PreIncrementExpression or SyntaxKind.PreDecrementExpression
            or SyntaxKind.PostIncrementExpression or SyntaxKind.PostDecrementExpression;

    private static ReceiverKind Classify(SemanticModel model, ExpressionSyntax expression)
    {
        var typeInfo = model.GetTypeInfo(expression);
        var type = typeInfo.Type ?? typeInfo.ConvertedType;

        if (type is null ||
            type.TypeKind == TypeKind.Error ||
            type.TypeKind == TypeKind.Dynamic ||
            type.SpecialType == SpecialType.System_Object)
        {
            return ReceiverKind.Unresolved;
        }

        for (var current = type; current is not null; current = current.BaseType)
        {
            if (string.Equals(current.Name, nameof(OmnichannelActivity), StringComparison.Ordinal))
            {
                return ReceiverKind.Activity;
            }
        }

        return ReceiverKind.OtherDocument;
    }

    /// <summary>
    /// Compiles every scanned source so receivers can be classified by their resolved type.
    /// </summary>
    /// <returns>The compilation together with the syntax tree of each scanned file.</returns>
    private static ScannedSources CompileScannedSources()
    {
        var trees = new Dictionary<string, SyntaxTree>(StringComparer.Ordinal);

        foreach (var file in EnumerateContactCenterSources())
        {
            trees[file] = CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file);
        }

        // The projector lives in a module the scan deliberately excludes from the violation sweep, but the
        // known-positive control still has to classify its receivers.
        foreach (var projector in Directory.EnumerateFiles(Path.Combine(FindRepositoryRoot(), "src"), ProjectorFileName, SearchOption.AllDirectories))
        {
            if (!IsGeneratedPath(projector))
            {
                trees.TryAdd(projector, CSharpSyntaxTree.ParseText(File.ReadAllText(projector), path: projector));
            }
        }

        // The scanned projects are compiled from source, so their own assemblies are left out to keep every
        // type they declare unambiguous.
        var excluded = _scannedAssemblyNames.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var references = new List<MetadataReference>();

        // The shared frameworks come first, because the test's own output folder carries neither the base class
        // library nor ASP.NET Core and a compilation without them types nothing at all.
        foreach (var folder in EnumerateReferenceFolders())
        {
            foreach (var path in Directory.EnumerateFiles(folder, "*.dll"))
            {
                var name = Path.GetFileNameWithoutExtension(path);

                if (excluded.Contains(name) || !seen.Add(name))
                {
                    continue;
                }

                var reference = TryReference(path);

                if (reference is not null)
                {
                    references.Add(reference);
                }
            }
        }

        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true);

        // The SDK implements its implicit usings by generating a file, so the scan generates the same one rather
        // than leaving every source unable to name a task or a cancellation token.
        var implicitUsings = CSharpSyntaxTree.ParseText(
            string.Concat(_implicitUsings.Select(name => $"global using global::{name};{Environment.NewLine}")));

        var compilation = CSharpCompilation.Create(
            "ContactCenterAuthorityScan",
            trees.Values.Append(implicitUsings),
            references,
            options);

        return new ScannedSources(compilation, trees);
    }

    /// <summary>
    /// Enumerates the folders whose assemblies the scan compiles against, shared frameworks first.
    /// </summary>
    /// <returns>The reference folders, in the order their assemblies win.</returns>
    private static IEnumerable<string> EnumerateReferenceFolders()
    {
        var runtimeDirectory = RuntimeEnvironment.GetRuntimeDirectory();

        yield return runtimeDirectory;

        var sharedRoot = Path.GetDirectoryName(Path.GetDirectoryName(runtimeDirectory.TrimEnd(Path.DirectorySeparatorChar)));
        var aspNetCoreRoot = sharedRoot is null ? null : Path.Combine(sharedRoot, "Microsoft.AspNetCore.App");

        if (aspNetCoreRoot is not null && Directory.Exists(aspNetCoreRoot))
        {
            var version = new DirectoryInfo(runtimeDirectory.TrimEnd(Path.DirectorySeparatorChar)).Name;
            var matching = Path.Combine(aspNetCoreRoot, version);

            if (Directory.Exists(matching))
            {
                yield return matching;
            }
            else
            {
                var latest = Directory.EnumerateDirectories(aspNetCoreRoot).OrderBy(path => path, StringComparer.Ordinal).LastOrDefault();

                if (latest is not null)
                {
                    yield return latest;
                }
            }
        }

        yield return AppContext.BaseDirectory;
    }

    private static PortableExecutableReference TryReference(string path)
    {
        try
        {
            return MetadataReference.CreateFromFile(path);
        }
        catch (Exception exception) when (exception is BadImageFormatException or IOException)
        {
            return null;
        }
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
            AssignedToId = "agent-9",
            Attempts = 4,
        }.RestorePersistedAssignmentStatus(ActivityAssignmentStatus.Assigned);

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
            state => state.RestorePersistedAssignmentStatus(ActivityAssignmentStatus.Reserved),
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
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
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
        queueItem.RestorePersistedStatus(QueueItemStatus.Waiting);
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
            .Column<DateTime>("EnqueuedUtc", column => column.NotNull())
            .Column<DateTime>("DequeuedUtc", column => column.Nullable()),
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
            .Column<string>("AssignedToId", column => column.WithLength(26))
            .Column<DateTime>("ModifiedUtc", column => column.Nullable()),
            collection: ContactCenterConstants.CollectionName);

        await schemaBuilder.CreateMapIndexTableAsync<ActivityReservationIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("ActivityItemId", column => column.WithLength(26))
            .Column<string>("ActivityClaimKey", column => column.NotNull().Unique().WithLength(26))
            .Column<string>("AgentId", column => column.WithLength(26))
            .Column<string>("AgentClaimKey", column => column.NotNull().Unique().WithLength(26))
            .Column<string>("Status", column => column.WithLength(50))
            .Column<DateTime>("ExpiresUtc", column => column.NotNull())
            .Column<DateTime>("ModifiedUtc", column => column.Nullable()),
            collection: ContactCenterConstants.CollectionName);

        await transaction.CommitAsync(TestContext.Current.CancellationToken);
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

    /// <summary>
    /// How a routing-owned member write's receiver was classified.
    /// </summary>
    private enum ReceiverKind
    {
        /// <summary>
        /// The compiler could not give the receiver a type, so it cannot be shown not to be a CRM activity.
        /// </summary>
        Unresolved,

        /// <summary>
        /// The receiver is a CRM activity.
        /// </summary>
        Activity,

        /// <summary>
        /// The receiver is a document that owns the member itself.
        /// </summary>
        OtherDocument,
    }

    /// <summary>
    /// One routing-owned field write the authority scan reports.
    /// </summary>
    /// <param name="Kind">How the receiver of the write was classified.</param>
    /// <param name="Description">The human-readable report, naming the file, line, member, and receiver.</param>
    private sealed record RoutingStateWrite(ReceiverKind Kind, string Description);

    /// <summary>
    /// The compiled sources the authority scan inspects.
    /// </summary>
    /// <param name="Compilation">The compilation the scanned trees belong to.</param>
    /// <param name="TreesByPath">The syntax tree of each scanned file, keyed by its full path.</param>
    private sealed record ScannedSources(CSharpCompilation Compilation, IReadOnlyDictionary<string, SyntaxTree> TreesByPath);
}

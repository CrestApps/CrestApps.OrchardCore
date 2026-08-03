using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Providers;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.Environment.Shell.Configuration;
using YesSql;
using YesSql.Provider.Sqlite;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Reports;

/// <summary>
/// Proves that <see cref="EnterpriseInteractionReportProvider"/> cannot leak per-execution state across report
/// requests, even though it captures the tenant's agent names and absent capabilities in instance fields
/// (<c>_agentUserNames</c> and <c>_absentFeatureIds</c>).
/// </summary>
/// <remarks>
/// Two leak shapes were hypothesized for the report providers. The first is a cross-request leak: two concurrent
/// report requests corrupting each other's captured state. The second is stale state left on a reused instance: a
/// later request reading the capabilities or agent names captured by an earlier one. The captured fields are safe from
/// both, and these tests prove it without a broad refactor (the plan's directive was to prove the concern before
/// refactoring, and to leave the code unchanged when no defect is observable).
///
/// The cross-request shape is closed structurally: every report provider is registered with a scoped lifetime, so each
/// request scope resolves its own instance and no two concurrent requests can share the mutable fields. That lifetime
/// is asserted directly here, so a future singleton misregistration — the only way concurrent requests could share an
/// instance — fails the build. The stale-reuse shape is closed behaviourally: <c>RunAsync</c> re-captures both fields
/// from the injected tenant-scoped services on every call, so reusing one instance for a second request with different
/// capabilities and agents produces the second request's results, never the first's.
/// </remarks>
public sealed class EnterpriseInteractionReportConcurrencyTests
{
    private const int InteractionIdColumn = 1;
    private const int AgentColumn = 6;

    private static readonly DateTime _windowAFrom = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _windowATo = new(2026, 3, 31, 23, 59, 59, DateTimeKind.Utc);
    private static readonly DateTime _windowBFrom = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _windowBTo = new(2026, 6, 30, 23, 59, 59, DateTimeKind.Utc);

    [Fact]
    public void ConfigureServices_RegistersEveryReportProviderWithScopedLifetime()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        new AnalyticsStartup(
            new EmptyShellConfiguration(),
            new PassThroughStringLocalizer<AnalyticsStartup>()).ConfigureServices(services);

        // Assert
        var reportDescriptors = services
            .Where(descriptor => descriptor.ServiceType == typeof(IReport))
            .ToArray();

        Assert.NotEmpty(reportDescriptors);

        // A scoped lifetime is what prevents two concurrent requests from ever sharing one provider instance, so the
        // captured fields cannot cross requests. A singleton registration would reintroduce that risk.
        Assert.All(reportDescriptors, descriptor => Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime));
    }

    [Fact]
    public async Task RunAsync_WhenOneInstanceIsReusedAcrossRequests_DoesNotCarryStaleCapturedState()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, databasePath) = await CreateSeededStoreAsync(cancellationToken);

        try
        {
            await using var session = store.CreateSession();
            var guard = new FlippingCapabilityGuard();
            var agentManager = new Mock<IAgentProfileManager>();
            var provider = CreateProvider(session, guard, agentManager.Object);

            // First request: the voice capability is absent (its columns must drop) and only agent-a is known.
            guard.MissingFeatures = [ContactCenterConstants.Feature.Voice];
            SetAgents(agentManager, ("agent-a", "Alice"));
            var firstDocument = await provider.RunAsync(CreateContext(_windowAFrom, _windowATo), cancellationToken);

            // Second request on the same instance: the capability is now present and only agent-b is known. Stale
            // captured state would keep dropping the voice columns or fail to resolve agent-b's name.
            guard.MissingFeatures = [];
            SetAgents(agentManager, ("agent-b", "Bob"));
            var secondDocument = await provider.RunAsync(CreateContext(_windowBFrom, _windowBTo), cancellationToken);

            // Assert
            AssertReport(firstDocument, voicePresent: false, expectedAgent: "Alice", expectedIds: ["a-1", "a-2"]);
            AssertReport(secondDocument, voicePresent: true, expectedAgent: "Bob", expectedIds: ["b-1", "b-2", "b-3"]);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static void AssertReport(
        ReportDocument document,
        bool voicePresent,
        string expectedAgent,
        string[] expectedIds)
    {
        var section = Assert.Single(document.Sections);

        // The provider and transfer columns are produced only when the voice capability is present, so their presence
        // is a direct observation of the instance's captured absent-capability set.
        Assert.Equal(voicePresent, section.Columns.Any(column => column.Label == "Provider"));
        Assert.Equal(voicePresent, section.Columns.Any(column => column.Label == "Transfers"));

        var actualIds = section.Rows
            .Select(row => row.Cells[InteractionIdColumn])
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expectedIds, actualIds);

        // Every agent cell is resolved through the captured agent-name map; a crossed or stale map would resolve to a
        // different name or to the raw agent id. The cell carries an encoded display-name token, so decode it back to
        // the captured username before comparing.
        Assert.All(section.Rows, row =>
        {
            Assert.True(ReportValue.TryGetUserName(row.Cells[AgentColumn], out var userName));
            Assert.Equal(expectedAgent, userName);
        });
    }

    private static EnterpriseInteractionReportProvider CreateProvider(
        ISession session,
        IContactCenterReportCapabilityGuard guard,
        IAgentProfileManager agentManager)
    {
        var queueManager = new Mock<IActivityQueueManager>();
        queueManager
            .Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<ActivityQueue>)[]);

        var definition = new EnterpriseInteractionReportDefinition(
            "interaction-detail",
            () => new LocalizedString("Interaction detail", "Interaction detail"),
            () => new LocalizedString("Interaction detail", "Interaction detail"),
            EnterpriseInteractionReportKind.InteractionDetail,
            "Interactions",
            []);

        return new EnterpriseInteractionReportProvider(
            session,
            queueManager.Object,
            agentManager,
            definition,
            guard,
            new PassThroughStringLocalizer<EnterpriseInteractionReportProvider>(),
            TimeSpan.FromDays(400));
    }

    private static void SetAgents(Mock<IAgentProfileManager> agentManager, params (string Id, string Name)[] agents)
    {
        agentManager
            .Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<AgentProfile>)
                [.. agents.Select(agent => new AgentProfile { ItemId = agent.Id, UserName = agent.Name })]);
    }

    private static ReportContext CreateContext(DateTime fromUtc, DateTime toUtc)
    {
        return new ReportContext(new ReportFilter
        {
            FromUtc = fromUtc,
            ToUtc = toUtc,
        });
    }

    private static async Task<(IStore Store, string DatabasePath)> CreateSeededStoreAsync(CancellationToken cancellationToken)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "ReportConcurrencyData");
        Directory.CreateDirectory(directory);

        var databasePath = Path.Combine(directory, $"report-concurrency-{Guid.NewGuid():N}.db");
        var store = StoreFactory.Create(configuration =>
            configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes([new InteractionIndexProvider()]);

        await store.InitializeAsync(cancellationToken);
        await store.InitializeCollectionAsync(ContactCenterConstants.CollectionName, cancellationToken);

        await using (var migrationSession = store.CreateSession())
        {
            var transaction = await migrationSession.BeginTransactionAsync(cancellationToken);
            await InteractionQueryPlanFixture.MigrateAsync(store.Configuration, transaction);
            await transaction.CommitAsync(cancellationToken);
        }

        await using var session = store.CreateSession();

        Save(session, "a-1", "agent-a", new DateTime(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc));
        Save(session, "a-2", "agent-a", new DateTime(2026, 3, 11, 9, 0, 0, DateTimeKind.Utc));
        Save(session, "b-1", "agent-b", new DateTime(2026, 6, 10, 9, 0, 0, DateTimeKind.Utc));
        Save(session, "b-2", "agent-b", new DateTime(2026, 6, 11, 9, 0, 0, DateTimeKind.Utc));
        Save(session, "b-3", "agent-b", new DateTime(2026, 6, 12, 9, 0, 0, DateTimeKind.Utc));

        await session.SaveChangesAsync(cancellationToken);

        return (store, databasePath);
    }

    private static void Save(ISession session, string itemId, string agentId, DateTime createdUtc)
    {
        session.Save(
            new Interaction
            {
                ItemId = itemId,
                AgentId = agentId,
                Channel = InteractionChannel.Voice,
                Direction = InteractionDirection.Inbound,
                CreatedUtc = createdUtc,
            }.RestorePersistedStatus(InteractionStatus.Ended),
            collection: ContactCenterConstants.CollectionName);
    }

    private sealed class FlippingCapabilityGuard : IContactCenterReportCapabilityGuard
    {
        public IReadOnlyCollection<string> MissingFeatures { get; set; } = [];

        public ValueTask<IReadOnlyCollection<string>> GetMissingFeaturesAsync(
            IReadOnlyCollection<string> requiredFeatureIds,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<IReadOnlyCollection<string>>(
                [.. requiredFeatureIds.Where(featureId => MissingFeatures.Contains(featureId))]);
        }

        public ReportDocument DescribeUnavailable(IReadOnlyCollection<string> missingFeatureIds)
        {
            return new ReportDocument();
        }
    }

    private sealed class EmptyShellConfiguration : IShellConfiguration
    {
        private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration = new ConfigurationBuilder().Build();

        public string this[string key]
        {
            get => _configuration[key];
            set => _configuration[key] = value;
        }
        public IEnumerable<IConfigurationSection> GetChildren()
            => _configuration.GetChildren();

        public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken()
            => _configuration.GetReloadToken();

        public IConfigurationSection GetSection(string key)
            => _configuration.GetSection(key);
    }
}

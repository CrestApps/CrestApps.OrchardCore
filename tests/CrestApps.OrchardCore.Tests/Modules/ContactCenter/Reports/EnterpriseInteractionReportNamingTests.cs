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
using Microsoft.Extensions.Localization;
using Moq;
using YesSql;
using YesSql.Provider.Sqlite;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Reports;

/// <summary>
/// A call that dialled the platform's own number was routed straight to an agent, never answered, and failed. The
/// interaction reports listed it under the raw internal queue id "__cc-direct-routing__" and named its agent
/// "(Unknown agent)", although no agent ever took it; every other row showed its queue's id instead of its name. These
/// pin that the detail report names real queues, calls direct-to-agent routing what it is, and says "(No agent)" for
/// work no agent took, while an agent that no longer exists is still reported as unknown.
/// </summary>
public sealed class EnterpriseInteractionReportNamingTests
{
    private const int QueueColumn = 5;
    private const int AgentColumn = 6;

    private static readonly DateTime _from = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _to = new(2026, 9, 30, 23, 59, 59, DateTimeKind.Utc);

    [Fact]
    public async Task InteractionDetail_NamesQueuesAndAgentsAsTheyAre()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, databasePath) = await CreateStoreAsync(cancellationToken,
            ("queued", "queue-1", "agent-1"),
            ("direct", ContactCenterConstants.DirectRouting.QueueId, null),
            ("orphan", "queue-1", "agent-gone"));

        try
        {
            await using var session = store.CreateSession();
            var provider = CreateProvider(
                session,
                new ActivityQueue { ItemId = "queue-1", Name = "Support" },
                new AgentProfile { ItemId = "agent-1", UserName = "mike" });

            // Act
            var document = await provider.RunAsync(CreateContext(), cancellationToken);

            // Assert
            var rows = Assert.Single(document.Sections).Rows.ToDictionary(row => row.Cells[1]);

            Assert.Equal("Support", rows["queued"].Cells[QueueColumn]);
            Assert.True(ReportValue.TryGetUserName(rows["queued"].Cells[AgentColumn], out var userName));
            Assert.Equal("mike", userName);

            Assert.Equal("Direct to agent", rows["direct"].Cells[QueueColumn]);
            Assert.Equal("(No agent)", rows["direct"].Cells[AgentColumn]);

            Assert.Equal("(Unknown agent)", rows["orphan"].Cells[AgentColumn]);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task AgentUsageBilling_GroupsWorkNoAgentTookUnderNoAgent()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, databasePath) = await CreateStoreAsync(cancellationToken,
            ("direct", ContactCenterConstants.DirectRouting.QueueId, null));

        try
        {
            await using var session = store.CreateSession();
            var provider = CreateProvider(session, kind: EnterpriseInteractionReportKind.AgentUsageBilling);

            // Act
            var document = await provider.RunAsync(CreateContext(), cancellationToken);

            // Assert
            var cells = document.Sections.SelectMany(section => section.Rows).SelectMany(row => row.Cells).ToArray();

            Assert.Contains("(No agent)", cells);
            Assert.DoesNotContain("(Unknown agent)", cells);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static EnterpriseInteractionReportProvider CreateProvider(
        ISession session,
        ActivityQueue queue = null,
        AgentProfile agent = null,
        EnterpriseInteractionReportKind kind = EnterpriseInteractionReportKind.InteractionDetail)
    {
        var queueManager = new Mock<IActivityQueueManager>();
        queueManager
            .Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<ActivityQueue>)(queue is null ? [] : [queue]));

        var agentManager = new Mock<IAgentProfileManager>();
        agentManager
            .Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<AgentProfile>)(agent is null ? [] : [agent]));

        var guard = new Mock<IContactCenterReportCapabilityGuard>();
        guard
            .Setup(value => value.GetMissingFeaturesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var definition = new EnterpriseInteractionReportDefinition(
            "naming",
            () => new LocalizedString("Naming", "Naming"),
            () => new LocalizedString("Naming", "Naming"),
            kind,
            "Interactions",
            []);

        return new EnterpriseInteractionReportProvider(
            session,
            queueManager.Object,
            agentManager.Object,
            definition,
            guard.Object,
            new PassThroughStringLocalizer<EnterpriseInteractionReportProvider>(),
            TimeSpan.FromDays(400));
    }

    private static ReportContext CreateContext()
    {
        var filter = new ReportFilter();
        filter.SetDateRange(new ReportDateRange { FromUtc = _from, ToUtc = _to });

        return new ReportContext(filter);
    }

    private static async Task<(IStore Store, string DatabasePath)> CreateStoreAsync(
        CancellationToken cancellationToken,
        params (string ItemId, string QueueId, string AgentId)[] interactions)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "ReportNamingData");
        Directory.CreateDirectory(directory);

        var databasePath = Path.Combine(directory, $"report-naming-{Guid.NewGuid():N}.db");
        var store = StoreFactory.Create(configuration =>
            configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes([new InteractionIndexProvider()]);

        await store.InitializeAsync(cancellationToken);
        await store.InitializeCollectionAsync(ContactCenterStorage.CollectionName, cancellationToken);

        await using (var migrationSession = store.CreateSession())
        {
            var transaction = await migrationSession.BeginTransactionAsync(cancellationToken);
            await InteractionQueryPlanFixture.MigrateAsync(store, transaction);
            await transaction.CommitAsync(cancellationToken);
        }

        await using var session = store.CreateSession();

        foreach (var (itemId, queueId, agentId) in interactions)
        {
            await session.SaveAsync(
                new Interaction
                {
                    ItemId = itemId,
                    QueueId = queueId,
                    AgentId = agentId,
                    Channel = InteractionChannel.Voice,
                    Direction = InteractionDirection.Inbound,
                    CreatedUtc = new DateTime(2026, 9, 24, 14, 6, 15, DateTimeKind.Utc),
                }.RestorePersistedStatus(InteractionStatus.Failed),
                collection: ContactCenterStorage.CollectionName);
        }

        await session.SaveChangesAsync(cancellationToken);

        return (store, databasePath);
    }
}

using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.AI;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models.Dispatch;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.AI;

/// <summary>
/// The dispatch diagnostics, stuck work, configuration and roster tools.
/// </summary>
public sealed class QueueHealthToolsTests
{
    private const string CampaignId = "leadgen";
    private const string QueueId = "q-preview";

    private static readonly string _campaignQueueId = ContactCenterConstants.CampaignQueue.CreateId(CampaignId);

    [Fact]
    public async Task DispatchDiagnostics_WhenACampaignHasDialableWorkAndNobodyIsSignedIn_ListsItAsQuiet()
    {
        // Arrange
        var host = DispatchHost(Diagnosis(dialable: 5, waiting: 5), []);

        // Act
        var result = await host.RunAsync(new DispatchDiagnosticsTool());

        // Assert
        Assert.Equal("Lead Generation", result.GetProperty("quietQueues").GetProperty("items")[0].GetString());
        Assert.Contains("work due but nothing happening", result.GetProperty("verdict").GetString(), StringComparison.Ordinal);
        Assert.Contains("NobodySignedIn", ContactCenterToolHost.FindingCodes(result.GetProperty("queues").GetProperty("items")[0]));
    }

    [Fact]
    public async Task DispatchDiagnostics_WhenAgentsAreAvailableButRoutingNeverDecided_SaysRoutingMayNotBeRunning()
    {
        // Arrange
        var agent = ContactCenterToolHost.Agent("a1", "agent1", AgentPresenceStatus.Available);
        agent.CampaignIds = [CampaignId];
        var host = DispatchHost(Diagnosis(dialable: 5, waiting: 5), [agent]);

        // Act
        var result = await host.RunAsync(new DispatchDiagnosticsTool());

        // Assert
        var queue = result.GetProperty("queues").GetProperty("items")[0];
        Assert.Contains("Routing.Quiet", ContactCenterToolHost.FindingCodes(queue));
        Assert.Contains("never recorded a decision", queue.GetProperty("findings").EnumerateArray().First(finding => finding.GetProperty("code").GetString() == "Routing.Quiet").GetProperty("summary").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DispatchDiagnostics_WhenTheQueueIsTurnedOff_SaysThat()
    {
        // Arrange
        var diagnosis = Diagnosis(dialable: 0, waiting: 4);
        diagnosis.QueueBlocker = DispatchBlocker.QueueDisabled;
        var host = DispatchHost(diagnosis, []);

        // Act
        var result = await host.RunAsync(new DispatchDiagnosticsTool());

        // Assert
        Assert.Contains("Queue.QueueDisabled", ContactCenterToolHost.FindingCodes(result.GetProperty("queues").GetProperty("items")[0]));
    }

    [Fact]
    public async Task DispatchDiagnostics_WhenNothingIsPending_SaysThereIsNoWorkInsteadOfListingQueues()
    {
        // Arrange
        var host = DispatchHost(Diagnosis(dialable: 0, waiting: 0, pending: 0), []);

        // Act
        var result = await host.RunAsync(new DispatchDiagnosticsTool());

        // Assert
        Assert.Equal(0, result.GetProperty("queuesWithWork").GetInt32());
        Assert.Contains("no work for routing", result.GetProperty("verdict").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Quiet_IsOnlyTrueWhenWorkIsDueAndSomethingSpecificExplainsWhyNothingHappens()
    {
        // Arrange
        var facts = new QueueDispatchFacts(Diagnosis(dialable: 3, waiting: 3), "Lead Generation", ContactCenterToolHost.Now.AddMinutes(-3), 2, 2, null, null, null, ContactCenterToolHost.Now);

        // Act
        var recent = DispatchAnalyzer.Analyze(facts);
        var stale = DispatchAnalyzer.Analyze(facts with { LastRoutingDecisionUtc = ContactCenterToolHost.Now.AddMinutes(-30) });

        // Assert
        Assert.False(DispatchAnalyzer.IsQuiet(recent));
        Assert.True(DispatchAnalyzer.IsQuiet(stale));
    }

    [Fact]
    public async Task StuckWork_WhenAnExpiredReservationStillHoldsAWaitingItem_SaysItBlocksTheQueue()
    {
        // Arrange
        var reservation = new ActivityReservation
        {
            ItemId = "res-1",
            ActivityItemId = "act-1",
            QueueId = QueueId,
            AgentId = "agent-1",
            CreatedUtc = ContactCenterToolHost.Now.AddMinutes(-30),
            ExpiresUtc = ContactCenterToolHost.Now.AddMinutes(-20),
        };
        var host = StuckHost(
            expired: [reservation],
            itemStatus: QueueItemStatus.Waiting,
            orphans: [new OrphanedActivity("act-8", CampaignId, "Dialing", ContactCenterToolHost.Now.AddHours(-1), WouldRequeue: true), new OrphanedActivity("act-9", CampaignId, "InProgress", null, WouldRequeue: false)]);

        // Act
        var result = await host.RunAsync(new StuckWorkTool());

        // Assert
        var codes = ContactCenterToolHost.FindingCodes(result);
        Assert.Contains("ReservationBlocksWaitingItem", codes);
        Assert.Contains("OrphanedActivities", codes);
        Assert.StartsWith("Found stuck work", result.GetProperty("verdict").GetString(), StringComparison.Ordinal);
        Assert.Equal("act-1", result.GetProperty("expiredReservations").GetProperty("items")[0].GetProperty("activityId").GetString());
        Assert.Equal(2, result.GetProperty("orphanedActivities").GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task StuckWork_WhenNothingIsStuck_SaysSo()
    {
        // Arrange
        var host = StuckHost(expired: [], itemStatus: null, orphans: []);

        // Act
        var result = await host.RunAsync(new StuckWorkTool());

        // Assert
        Assert.StartsWith("Nothing is stuck", result.GetProperty("verdict").GetString(), StringComparison.Ordinal);
        Assert.Empty(result.GetProperty("findings").EnumerateArray());
    }

    [Fact]
    public async Task ListConfiguration_ListsOfferTimeoutsAndWhetherEachIsTurnedOn()
    {
        // Arrange
        var host = new ContactCenterToolHost()
            .WithQueues(new ActivityQueue { ItemId = QueueId, Name = "Preview", Enabled = false, ReservationTimeoutSeconds = 30 })
            .WithCampaigns((CampaignId, "Lead Generation"))
            .WithAgents();
        var profiles = new Mock<IDialerProfileManager>();
        profiles
            .Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DialerProfile> { new() { ItemId = "p1", Name = "Preview dialing", Mode = DialerMode.Preview, Enabled = true, PreviewOfferTimeoutSeconds = 45, MaxAttempts = 3, RetryDelayMinutes = 60 } });
        host.Services.AddSingleton(profiles.Object);

        // Act
        var result = await host.RunAsync(new ListConfigurationTool());

        // Assert
        var profile = result.GetProperty("profiles").GetProperty("items")[0];
        Assert.Equal("Preview", profile.GetProperty("mode").GetString());
        Assert.Equal(45, profile.GetProperty("offerTimeoutSeconds").GetInt32());
        Assert.Equal(60, profile.GetProperty("retryDelayMinutes").GetInt32());

        var queue = result.GetProperty("queues").GetProperty("items")[0];
        Assert.Equal(30, queue.GetProperty("offerTimeoutSeconds").GetInt32());
        Assert.False(queue.GetProperty("enabled").GetBoolean());
        Assert.Contains("hands nobody work", queue.GetProperty("note").GetString(), StringComparison.Ordinal);
        Assert.Equal("Lead Generation", result.GetProperty("campaigns").GetProperty("items")[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task AgentRoster_SaysWhoCanActuallyBeOfferedWorkNow()
    {
        // Arrange: ready is Available with a live session; ghost is Available with no session; away is signed in but Away.
        var ready = ContactCenterToolHost.Agent("ready", "ready", AgentPresenceStatus.Available, QueueId);
        var ghost = ContactCenterToolHost.Agent("ghost", "ghost", AgentPresenceStatus.Available, QueueId);
        var away = ContactCenterToolHost.Agent("away", "away", AgentPresenceStatus.Away, QueueId);
        var host = new ContactCenterToolHost();
        host.WithAvailability(
            [ready, ghost, away],
            [ContactCenterToolHost.LiveSession(ready, QueueId), ContactCenterToolHost.LiveSession(away, QueueId)],
            new Dictionary<string, int>(),
            queues: [new ActivityQueue { ItemId = QueueId, Name = "Preview", Enabled = true }]);

        // Act
        var result = await host.RunAsync(new AgentRosterTool());

        // Assert
        var verdict = result.GetProperty("verdict").GetString();
        Assert.Contains("3 of 3 agent(s) are signed in", verdict, StringComparison.Ordinal);
        Assert.Contains("2 of them are Available and 1 can be offered work right now", verdict, StringComparison.Ordinal);
        Assert.Contains("1 Available agent(s) have no live session", verdict, StringComparison.Ordinal);
        Assert.Equal(3, result.GetProperty("agents").GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task AgentRoster_WhenNobodyIsSignedIn_SaysSoAndHowToListEveryAgent()
    {
        // Arrange
        var agent = ContactCenterToolHost.Agent("idle", "idle", AgentPresenceStatus.Offline);
        var host = new ContactCenterToolHost();
        host.WithAvailability([agent], [], new Dictionary<string, int>());

        // Act
        var result = await host.RunAsync(new AgentRosterTool());

        // Assert
        Assert.Contains("No agent is signed in", result.GetProperty("verdict").GetString(), StringComparison.Ordinal);
        Assert.Contains("signedInOnly false", result.GetProperty("verdict").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AgentRoster_WhenThePresenceIsNotOneWeKnow_SaysWhichAreValid()
    {
        // Arrange
        var host = new ContactCenterToolHost();
        host.WithAvailability([], [], new Dictionary<string, int>());

        // Act
        var result = await host.RunAsync(new AgentRosterTool(), ("presence", "Vacationing"));

        // Assert
        Assert.Contains("Available", result.GetProperty("error").GetString(), StringComparison.Ordinal);
    }

    private static QueueDispatchDiagnostics Diagnosis(int dialable, int waiting, int? pending = null)
        => new()
        {
            QueueId = _campaignQueueId,
            CampaignId = CampaignId,
            PendingCount = pending ?? dialable + waiting,
            WaitingItemCount = waiting,
            DialableNowCount = dialable,
            EvaluatedCount = dialable,
            Buckets = dialable > 0 ? [new DispatchBucket { Blocker = DispatchBlocker.DialableNow, Count = dialable }] : [],
        };

    private static ContactCenterToolHost DispatchHost(QueueDispatchDiagnostics diagnosis, IReadOnlyList<AgentProfile> agents)
    {
        var host = new ContactCenterToolHost()
            .WithQueues()
            .WithCampaigns((CampaignId, "Lead Generation"))
            .WithAgents([.. agents])
            .WithDecisionLog();
        var diagnostics = new Mock<IDialerDispatchDiagnosticsService>();
        diagnostics
            .Setup(service => service.DiagnoseAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<string> ids, CancellationToken _) => (IReadOnlyList<QueueDispatchDiagnostics>)[.. ids.Select(_ => diagnosis)]);
        host.Services.AddSingleton(diagnostics.Object);

        return host;
    }

    private static ContactCenterToolHost StuckHost(IReadOnlyList<ActivityReservation> expired, QueueItemStatus? itemStatus, IReadOnlyList<OrphanedActivity> orphans)
    {
        var host = new ContactCenterToolHost()
            .WithQueues(new ActivityQueue { ItemId = QueueId, Name = "Preview", Enabled = true })
            .WithCampaigns((CampaignId, "Lead Generation"))
            .WithAgents();

        var reservations = new Mock<IActivityReservationManager>();
        reservations
            .Setup(manager => manager.GetExpiredAsync(It.IsAny<DateTime>(), It.IsAny<DateTime?>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExpiredReservationPage(expired, null, 0));
        reservations
            .Setup(manager => manager.GetActiveByActivityAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var items = new Mock<IQueueItemManager>();
        items
            .Setup(manager => manager.FindByActivityIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(itemStatus is null ? null : new QueueItem { ItemId = "qi", ActivityItemId = "act-1", QueueId = QueueId }.RestorePersistedStatus(itemStatus.Value));
        items
            .Setup(manager => manager.CountWaitingByQueueIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, int>());

        var recovery = new Mock<IOrphanedActivityRecoveryService>();
        recovery
            .Setup(service => service.FindAsync(It.IsAny<TimeSpan>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(orphans);

        host.Services.AddSingleton(reservations.Object);
        host.Services.AddSingleton(items.Object);
        host.Services.AddSingleton(recovery.Object);

        return host;
    }
}

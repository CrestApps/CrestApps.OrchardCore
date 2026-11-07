using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.AI;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.AI;

/// <summary>
/// Explains why an agent is not getting calls, with the real availability and routing code over doubles, so the reasons are
/// the ones routing acts on.
/// </summary>
public sealed class ExplainAgentNoCallsToolTests
{
    private const string QueueId = "q-preview";

    [Fact]
    public async Task InvokeAsync_WhenTheAgentIsPresentButHoldsAnUnfinishedActivity_SaysThatBlocksNewOffers()
    {
        // Arrange: Available with a live session, but a call they never finished is still open, and their one slot is taken.
        var agent = ContactCenterToolHost.Agent("oday", "OdayAyoub", AgentPresenceStatus.Available, QueueId);
        var session = ContactCenterToolHost.LiveSession(agent, QueueId);
        var open = new Interaction
        {
            ItemId = "int-9",
            ActivityItemId = "act-9",
            AgentId = agent.ItemId,
            CreatedUtc = ContactCenterToolHost.Now.AddHours(-2),
            StartedUtc = ContactCenterToolHost.Now.AddHours(-2),
        }.RestorePersistedStatus(InteractionStatus.Connected);
        var host = HostFor(agent, session, activeCounts: new Dictionary<string, int> { [agent.ItemId] = 1 }, active: [open], waiting: 5);

        // Act
        var result = await host.RunAsync(new ExplainAgentNoCallsTool(), ("agent", "OdayAyoub"));

        // Assert
        var codes = ContactCenterToolHost.FindingCodes(result);
        Assert.Contains("UnfinishedActivity", codes);
        Assert.Equal("certain", result.GetProperty("findings")[0].GetProperty("likelihood").GetString());
        Assert.Contains("act-9", result.GetProperty("verdict").GetString(), StringComparison.Ordinal);
        Assert.Contains("not getting calls", result.GetProperty("verdict").GetString(), StringComparison.Ordinal);
        Assert.False(result.GetProperty("queues").GetProperty("items")[0].GetProperty("eligibleNow").GetBoolean());
    }

    [Fact]
    public async Task InvokeAsync_WhenTheLastCallEndedButTheAfterCallWorkWasNeverFinished_SaysSo()
    {
        // Arrange
        var agent = ContactCenterToolHost.Agent("oday", "OdayAyoub", AgentPresenceStatus.Available, QueueId);
        var wrapUp = new Interaction
        {
            ItemId = "int-7",
            ActivityItemId = "act-7",
            AgentId = agent.ItemId,
            CreatedUtc = ContactCenterToolHost.Now.AddHours(-5),
            WrapUpStartedUtc = ContactCenterToolHost.Now.AddHours(-5),
        }.RestorePersistedStatus(InteractionStatus.Ended);
        var host = HostFor(agent, ContactCenterToolHost.LiveSession(agent, QueueId), activeCounts: new Dictionary<string, int> { [agent.ItemId] = 1 }, wrapUps: [wrapUp], waiting: 3);

        // Act
        var result = await host.RunAsync(new ExplainAgentNoCallsTool(), ("agent", "oday ayoub"));

        // Assert
        var finding = result.GetProperty("findings").EnumerateArray().First(entry => entry.GetProperty("code").GetString() == "UnfinishedActivity");
        Assert.Contains("after-call work was never completed", finding.GetProperty("summary").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_WhenOffersRangOutAfterThirtySecondsAndTheAgentLostTheRecord_ExplainsTheExpiry()
    {
        // Arrange: the agent is Available and eligible, but the last two offers rang for 30 s unanswered and expired.
        var agent = ContactCenterToolHost.Agent("oday", "OdayAyoub", AgentPresenceStatus.Available, QueueId);
        var offers = new[]
        {
            OfferEvent(ContactCenterConstants.Events.OfferExpired, agent.ItemId, "act-1", ContactCenterToolHost.Now.AddMinutes(-40), ringSeconds: 30),
            OfferEvent(ContactCenterConstants.Events.OfferExpired, agent.ItemId, "act-2", ContactCenterToolHost.Now.AddMinutes(-10), ringSeconds: 30),
            OfferEvent(ContactCenterConstants.Events.OfferExpired, "someone-else", "act-3", ContactCenterToolHost.Now.AddMinutes(-5), ringSeconds: 30),
        };
        var host = HostFor(agent, ContactCenterToolHost.LiveSession(agent, QueueId), offers: offers, waiting: 4);

        // Act
        var result = await host.RunAsync(new ExplainAgentNoCallsTool(), ("agent", "OdayAyoub"));

        // Assert
        var expired = result.GetProperty("findings").EnumerateArray().First(entry => entry.GetProperty("code").GetString() == "OfferExpired");
        Assert.Equal("likely", expired.GetProperty("likelihood").GetString());
        Assert.Equal(2, expired.GetProperty("facts").GetProperty("count").GetInt32());
        Assert.Contains("after ringing 30 s", expired.GetProperty("summary").GetString(), StringComparison.Ordinal);
        Assert.Contains("act-2", expired.GetProperty("summary").GetString(), StringComparison.Ordinal);
        Assert.Contains("loses the reservation", expired.GetProperty("summary").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_WhenTheAgentIsEligibleAndNothingIsWaiting_SaysThereIsNothingToOfferInsteadOfBlamingTheAgent()
    {
        // Arrange: the preview queue drained, which is what left the agent idle.
        var agent = ContactCenterToolHost.Agent("oday", "OdayAyoub", AgentPresenceStatus.Available, QueueId);
        var host = HostFor(agent, ContactCenterToolHost.LiveSession(agent, QueueId), waiting: 0);

        // Act
        var result = await host.RunAsync(new ExplainAgentNoCallsTool(), ("agent", "OdayAyoub"));

        // Assert
        Assert.Contains("Queue.NothingWaiting", ContactCenterToolHost.FindingCodes(result));
        Assert.DoesNotContain(result.GetProperty("findings").EnumerateArray(), finding => finding.GetProperty("likelihood").GetString() == "certain");
        Assert.Contains("Nothing is waiting", result.GetProperty("verdict").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_WhenTheAgentIsSignedInNowhereAndHasNoSession_SaysBothAsCertain()
    {
        // Arrange
        var agent = new AgentProfile
        {
            ItemId = "oday",
            UserId = "user-oday",
            UserName = "OdayAyoub",
            PresenceStatus = AgentPresenceStatus.Offline,
            AllowedQueueIds = [QueueId],
        };
        var host = HostFor(agent, null);

        // Act
        var result = await host.RunAsync(new ExplainAgentNoCallsTool(), ("agent", "OdayAyoub"));

        // Assert
        var codes = ContactCenterToolHost.FindingCodes(result);
        Assert.Contains("NotSignedIn", codes);
        Assert.Contains("NoLiveSession", codes);
        Assert.Contains("NotAvailable", codes);
    }

    [Fact]
    public async Task InvokeAsync_WhenTheNameMatchesNobody_SaysSoAndPointsAtTheRoster()
    {
        // Arrange
        var agent = ContactCenterToolHost.Agent("oday", "OdayAyoub", AgentPresenceStatus.Available, QueueId);
        var host = HostFor(agent, ContactCenterToolHost.LiveSession(agent, QueueId));

        // Act
        var result = await host.RunAsync(new ExplainAgentNoCallsTool(), ("agent", "Nobody Here"));

        // Assert
        Assert.Contains("No agent matches", result.GetProperty("error").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_WhenTheSupervisorDoesNotOverseeTheAgentsQueues_RefusesToExplain()
    {
        // Arrange
        var agent = ContactCenterToolHost.Agent("oday", "OdayAyoub", AgentPresenceStatus.Available, QueueId);
        var host = HostFor(agent, ContactCenterToolHost.LiveSession(agent, QueueId));
        host.DoesNotOversee(QueueId);

        // Act
        var result = await host.RunAsync(new ExplainAgentNoCallsTool(), ("agent", "OdayAyoub"));

        // Assert
        Assert.Contains("you don't oversee", result.GetProperty("error").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Analyze_WhenOnlyOneQueueIsAskedAbout_FindingsCoverOnlyThatQueue()
    {
        // Arrange
        var diagnosis = new CrestApps.OrchardCore.ContactCenter.Reports.Models.AgentAvailabilityDiagnosis
        {
            Agent = ContactCenterToolHost.Agent("oday", "OdayAyoub", AgentPresenceStatus.Available, "q-a", "q-b"),
            SessionLive = true,
            HasSession = true,
            SignedInAnywhere = true,
            Queues =
            [
                new CrestApps.OrchardCore.ContactCenter.Reports.Models.AgentQueueEligibility { QueueId = "q-a", Name = "Alpha", Entitled = true, SignedIn = true, WaitingCount = 0, Capacity = 1 },
                new CrestApps.OrchardCore.ContactCenter.Reports.Models.AgentQueueEligibility { QueueId = "q-b", Name = "Beta", Entitled = true, SignedIn = true, WaitingCount = 0, Capacity = 1 },
            ],
        };

        // Act
        var analysis = AgentCallAnalyzer.Analyze(new AgentCallFacts(diagnosis, [], [], [], [], ContactCenterToolHost.Now, TimeSpan.FromHours(12), "Alpha", null));

        // Assert
        Assert.Single(analysis.Findings, finding => finding.Code == "Queue.NothingWaiting");
        Assert.Contains("Alpha", analysis.Findings.First(finding => finding.Code == "Queue.NothingWaiting").Summary, StringComparison.Ordinal);
    }

    private static ContactCenterToolHost HostFor(
        AgentProfile agent,
        AgentSession session,
        IReadOnlyDictionary<string, int> activeCounts = null,
        IReadOnlyList<Interaction> active = null,
        IReadOnlyList<Interaction> wrapUps = null,
        IReadOnlyList<InteractionEvent> offers = null,
        int waiting = 0)
    {
        var host = new ContactCenterToolHost();
        host.WithAvailability(
            [agent],
            session is null ? [] : [session],
            activeCounts ?? new Dictionary<string, int>(),
            active,
            wrapUps,
            offers,
            queues: [new ActivityQueue { ItemId = QueueId, Name = "Preview", Enabled = true }],
            waiting: new Dictionary<string, int> { [QueueId] = waiting });
        host.WithDecisionLog();

        return host;
    }

    private static InteractionEvent OfferEvent(string eventType, string agentId, string activityId, DateTime occurredUtc, double ringSeconds)
    {
        var offer = new InteractionEvent
        {
            ItemId = Guid.NewGuid().ToString("N"),
            EventType = eventType,
            AggregateType = nameof(ActivityReservation),
            AggregateId = "res-" + activityId,
            OccurredUtc = occurredUtc,
        };

        offer.SetData(new OfferLifecycleEventData
        {
            ReservationId = "res-" + activityId,
            ActivityItemId = activityId,
            QueueId = QueueId,
            AgentId = agentId,
            RingSeconds = ringSeconds,
            SettledUtc = occurredUtc,
        });

        return offer;
    }
}

using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using static CrestApps.OrchardCore.Tests.Modules.ContactCenter.Reports.AuditEvents;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Reports;

public sealed class CallHandlingMetricsTests
{
    private static readonly DateTime _start = new(2026, 9, 21, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _end = _start.AddHours(4);

    [Fact]
    public void Calculate_TalkTime_ExcludesEveryHold()
    {
        // Arrange: ten minutes connected, with a two-minute and a one-minute hold.
        var events = new[]
        {
            Call(ContactCenterConstants.Events.AgentLegAnswered, "call-1", _start, "agent-1"),
            Call(ContactCenterConstants.Events.CallHeld, "call-1", _start.AddMinutes(2), "agent-1", actorType: ContactCenterActorType.Agent),
            Call(ContactCenterConstants.Events.CallResumed, "call-1", _start.AddMinutes(4), "agent-1", durationSeconds: 120, actorType: ContactCenterActorType.Agent),
            Call(ContactCenterConstants.Events.CallHeld, "call-1", _start.AddMinutes(6), "agent-1"),
            Call(ContactCenterConstants.Events.CallResumed, "call-1", _start.AddMinutes(7), "agent-1"),
            Call(ContactCenterConstants.Events.CallEnded, "call-1", _start.AddMinutes(10)),
        };

        // Act
        var agent = Assert.Single(CallHandlingMetrics.Calculate(events, [], _end).Agents);

        // Assert
        Assert.Equal(1, agent.CallsHandled);
        Assert.Equal(600, agent.ConnectedSeconds);
        Assert.Equal(180, agent.HeldSeconds);
        Assert.Equal(2, agent.Holds);
        Assert.Equal(420, agent.TalkSeconds);
    }

    [Fact]
    public void Calculate_HoldStillOpenWhenTheCallEnds_CountsUpToTheEnd()
    {
        // Arrange
        var events = new[]
        {
            Call(ContactCenterConstants.Events.AgentLegAnswered, "call-1", _start, "agent-1"),
            Call(ContactCenterConstants.Events.CallHeld, "call-1", _start.AddMinutes(3), "agent-1"),
            Call(ContactCenterConstants.Events.CallEnded, "call-1", _start.AddMinutes(5)),
        };

        // Act
        var agent = Assert.Single(CallHandlingMetrics.Calculate(events, [], _end).Agents);

        // Assert
        Assert.Equal(120, agent.HeldSeconds);
        Assert.Equal(180, agent.TalkSeconds);
    }

    [Fact]
    public void Calculate_CallStillConnectedAtTheEnd_IsCountedUpToIt()
    {
        // Arrange
        var events = new[]
        {
            Call(ContactCenterConstants.Events.AgentLegAnswered, "call-1", _end.AddMinutes(-5), "agent-1"),
        };

        // Act
        var agent = Assert.Single(CallHandlingMetrics.Calculate(events, [], _end).Agents);

        // Assert
        Assert.Equal(300, agent.ConnectedSeconds);
    }

    [Fact]
    public void Calculate_RingTime_ComesFromEachSettledOffer()
    {
        // Arrange
        var offers = new[]
        {
            Offer(ContactCenterConstants.Events.OfferPresented, "reservation-1", "call-1", "agent-1", _start),
            Offer(ContactCenterConstants.Events.OfferAccepted, "reservation-1", "call-1", "agent-1", _start, _start.AddSeconds(6)),
            Offer(ContactCenterConstants.Events.OfferPresented, "reservation-2", "call-2", "agent-1", _start.AddMinutes(1)),
            Offer(ContactCenterConstants.Events.OfferExpired, "reservation-2", "call-2", "agent-1", _start.AddMinutes(1), _start.AddMinutes(1).AddSeconds(20)),
            Offer(ContactCenterConstants.Events.OfferDeclined, "reservation-3", "call-3", "agent-1", _start.AddMinutes(2), _start.AddMinutes(2).AddSeconds(3)),
            Offer(ContactCenterConstants.Events.OfferPresented, "reservation-4", "call-4", "agent-1", _start.AddMinutes(3)),
        };

        // Act
        var agent = Assert.Single(CallHandlingMetrics.Calculate([], offers, _end).Agents);

        // Assert
        Assert.Equal(4, agent.OffersPresented);
        Assert.Equal(3, agent.OffersSettled);
        Assert.Equal(1, agent.OffersAccepted);
        Assert.Equal(1, agent.OffersMissed);
        Assert.Equal(1, agent.OffersDeclined);
        Assert.Equal(29, agent.RingSeconds, 6);
        Assert.Equal(6, agent.RingToAnswerSeconds, 6);
    }

    [Fact]
    public void Calculate_QueueWait_IsTheWaitTheQueueMeasured()
    {
        // Arrange
        var events = new[]
        {
            Call(ContactCenterConstants.Events.CallQueued, "call-1", _start, queueId: "queue-1"),
            Call(ContactCenterConstants.Events.CallDequeued, "call-1", _start.AddSeconds(45), queueId: "queue-1", durationSeconds: 44.5),
            Call(ContactCenterConstants.Events.CallQueued, "call-2", _start, queueId: "queue-1"),
            Call(ContactCenterConstants.Events.CallDequeued, "call-2", _start.AddSeconds(15), queueId: "queue-1"),
        };

        // Act
        var queue = Assert.Single(CallHandlingMetrics.Calculate(events, [], _end).Queues);

        // Assert
        Assert.Equal(2, queue.Queued);
        Assert.Equal(2, queue.AnsweredFromQueue);
        Assert.Equal(59.5, queue.AnsweredWaitSeconds, 6);
        Assert.Equal(44.5, queue.LongestWaitSeconds, 6);
    }

    [Fact]
    public void Calculate_Abandons_AreCountedAsAbandonsNeverAsAnswered()
    {
        // Arrange: the caller left the queue by hanging up, which also dequeues the call.
        var events = new[]
        {
            Call(ContactCenterConstants.Events.CallQueued, "call-1", _start, queueId: "queue-1"),
            Call(ContactCenterConstants.Events.CallDequeued, "call-1", _start.AddSeconds(80), queueId: "queue-1", durationSeconds: 80),
            Call(ContactCenterConstants.Events.CallAbandoned, "call-1", _start.AddSeconds(80), queueId: "queue-1", actorType: ContactCenterActorType.Customer),
            Call(ContactCenterConstants.Events.CallQueued, "call-2", _start, queueId: "queue-1"),
            Call(ContactCenterConstants.Events.CallDequeued, "call-2", _start.AddSeconds(10), queueId: "queue-1", durationSeconds: 10),

            // A caller who hung up before reaching any queue is still an abandon.
            Call(ContactCenterConstants.Events.CallAbandoned, "call-3", _start.AddSeconds(5), actorType: ContactCenterActorType.Customer),
        };

        // Act
        var metrics = CallHandlingMetrics.Calculate(events, [], _end);

        // Assert
        Assert.Equal(2, metrics.Abandoned);
        var queue = Assert.Single(metrics.Queues, queue => queue.QueueId == "queue-1");
        Assert.Equal(2, queue.Queued);
        Assert.Equal(1, queue.AnsweredFromQueue);
        Assert.Equal(1, queue.Abandoned);
        Assert.Equal(0.5, queue.AbandonRate, 6);
        Assert.Equal(80, queue.AbandonedWaitSeconds, 6);
        Assert.Equal(10, queue.AnsweredWaitSeconds, 6);
    }

    [Fact]
    public void Calculate_ACallTakenOutOfTheQueueForVoicemail_IsNotAnsweredFromTheQueue()
    {
        // Arrange: the offer went unanswered and routing removed the call from the queue to send it to voicemail.
        var events = new[]
        {
            Call(ContactCenterConstants.Events.CallQueued, "call-1", _start, queueId: "queue-1"),
            Call(ContactCenterConstants.Events.CallDequeued, "call-1", _start.AddSeconds(73), queueId: "queue-1", durationSeconds: 73, state: nameof(QueueItemStatus.Removed)),
            Call(ContactCenterConstants.Events.CallQueued, "call-2", _start, queueId: "queue-1"),
            Call(ContactCenterConstants.Events.CallDequeued, "call-2", _start.AddSeconds(10), queueId: "queue-1", durationSeconds: 10, state: nameof(QueueItemStatus.Assigned)),
        };

        // Act
        var queue = Assert.Single(CallHandlingMetrics.Calculate(events, [], _end).Queues);

        // Assert
        Assert.Equal(2, queue.Queued);
        Assert.Equal(1, queue.AnsweredFromQueue);
        Assert.Equal(10, queue.AnsweredWaitSeconds, 6);
        Assert.Equal(0, queue.Abandoned);
    }

    [Fact]
    public void Calculate_WhenFilteredToAnAgent_CountsOnlyTheirCallsAndOffers()
    {
        // Arrange
        var events = new[]
        {
            Call(ContactCenterConstants.Events.AgentLegAnswered, "call-1", _start, "agent-1"),
            Call(ContactCenterConstants.Events.CallEnded, "call-1", _start.AddMinutes(1)),
            Call(ContactCenterConstants.Events.AgentLegAnswered, "call-2", _start, "agent-2"),
            Call(ContactCenterConstants.Events.CallEnded, "call-2", _start.AddMinutes(2)),
        };
        var offers = new[]
        {
            Offer(ContactCenterConstants.Events.OfferAccepted, "reservation-2", "call-2", "agent-2", _start, _start.AddSeconds(4)),
        };

        // Act
        var agent = Assert.Single(CallHandlingMetrics.Calculate(events, offers, _end, agentId: "agent-1").Agents);

        // Assert
        Assert.Equal("agent-1", agent.AgentId);
        Assert.Equal(60, agent.TalkSeconds);
        Assert.Equal(0, agent.OffersPresented);
    }
}

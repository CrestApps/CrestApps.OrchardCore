using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Tests.Modules.ContactCenter.Integration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Over-dialing campaigns are paced as things happen: the events that change how many calls a campaign should have in
/// flight ask for its queue to be paced, and the requests for one queue are merged rather than piling up.
/// </summary>
public sealed class PredictivePacingTriggerTests
{
    private static readonly string _campaignQueue = ContactCenterConstants.CampaignQueue.CreateId("campaign-1");

    [Fact]
    public void AnAgentBecomingAvailable_RequestsTheCampaignsTheyAreSignedInto()
    {
        // Arrange
        var interactionEvent = new InteractionEvent { EventType = ContactCenterConstants.Events.AgentStateChanged };
        interactionEvent.SetData(new AgentStateChangedEventData
        {
            AgentId = "agent-1",
            CurrentState = AgentPresenceStatus.Available,
            QueueIds = ["queue-1"],
            CampaignIds = ["campaign-1", "campaign-2"],
        });

        // Act
        var queueIds = PredictivePacingTriggerHandler.ResolveQueueIds(interactionEvent);

        // Assert: only campaign queues, never an inbound queue.
        Assert.Equal(
            [_campaignQueue, ContactCenterConstants.CampaignQueue.CreateId("campaign-2")],
            queueIds.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void AnAgentBecomingBusy_RequestsNothing()
    {
        // Arrange
        var interactionEvent = new InteractionEvent { EventType = ContactCenterConstants.Events.AgentStateChanged };
        interactionEvent.SetData(new AgentStateChangedEventData
        {
            CurrentState = AgentPresenceStatus.Busy,
            CampaignIds = ["campaign-1"],
        });

        // Act + Assert
        Assert.Empty(PredictivePacingTriggerHandler.ResolveQueueIds(interactionEvent));
    }

    [Theory]
    [InlineData(ContactCenterConstants.Events.CallEnded)]
    [InlineData(ContactCenterConstants.Events.CallConnected)]
    [InlineData(ContactCenterConstants.Events.DialerCallAbandoned)]
    [InlineData(ContactCenterConstants.Events.DialerAgentConnectClaimed)]
    public void ACallEvent_RequestsItsCampaignQueue(string eventType)
    {
        // Arrange
        var interactionEvent = new InteractionEvent { EventType = eventType };
        interactionEvent.SetData(new CallLifecycleEventData { QueueId = _campaignQueue });

        // Act + Assert
        Assert.Equal([_campaignQueue], PredictivePacingTriggerHandler.ResolveQueueIds(interactionEvent));
    }

    [Theory]
    [InlineData(ContactCenterConstants.Events.AgentReleased)]
    [InlineData(ContactCenterConstants.Events.QueueItemAssigned)]
    public void AnOfferEvent_RequestsItsCampaignQueue_ButNotAnInboundQueue(string eventType)
    {
        // Arrange
        var campaignEvent = new InteractionEvent { EventType = eventType };
        campaignEvent.SetData(new OfferLifecycleEventData { QueueId = _campaignQueue });
        var inboundEvent = new InteractionEvent { EventType = eventType };
        inboundEvent.SetData(new OfferLifecycleEventData { QueueId = "queue-1" });

        // Act + Assert
        Assert.Equal([_campaignQueue], PredictivePacingTriggerHandler.ResolveQueueIds(campaignEvent));
        Assert.Empty(PredictivePacingTriggerHandler.ResolveQueueIds(inboundEvent));
    }

    [Fact]
    public async Task TheHandler_AsksTheSchedulerForEveryQueueTheEventNames()
    {
        // Arrange
        var scheduler = new RecordingPacingScheduler();
        var handler = new PredictivePacingTriggerHandler(scheduler);
        var interactionEvent = new InteractionEvent { EventType = ContactCenterConstants.Events.CallEnded };
        interactionEvent.SetData(new CallLifecycleEventData { QueueId = _campaignQueue });

        // Act
        await handler.HandleAsync(interactionEvent, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([_campaignQueue], scheduler.Requests);
    }

    [Fact]
    public void Requests_ForOneQueue_AreMergedIntoOneRun_AndAnInboundQueueIsNeverPaced()
    {
        // Arrange
        var deadlines = new RecordingDeadlineScheduler();
        var scheduler = new PredictivePacingScheduler(
            deadlines,
            new TestClock(),
            Options.Create(new ContactCenterPredictiveDialingOptions()),
            NullLogger<PredictivePacingScheduler>.Instance);

        // Act
        scheduler.Request(_campaignQueue);
        scheduler.Request(_campaignQueue);
        scheduler.Request("queue-1");

        // Assert
        Assert.Equal([PredictivePacingScheduler.GetDeadlineKey(_campaignQueue)], deadlines.Keys);
    }
}

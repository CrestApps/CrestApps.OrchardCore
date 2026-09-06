using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public class VoiceAgentHandoffServiceTests
{
    [Fact]
    public void CanHandle_OnlyPhone()
    {
        var harness = new Harness();

        Assert.True(harness.Service.CanHandle("Phone"));
        Assert.False(harness.Service.CanHandle("SMS"));
    }

    [Fact]
    public async Task ACallerLeftWaiting_HearsTheQueueImmediately_NotOnTheNextSweep()
    {
        // Arrange
        // The treatment sweep runs in bursts with a gap between them, so a caller seated during that gap heard
        // nothing at all. Live calls landed in it twice: handed off at 15:19:06 and gone by 15:19:37, and again
        // at 15:41:30 with the next sweep not starting until 15:42:32. A transferred caller is already on the
        // line, listening, having just been told a person is coming — silence there sounds like a dropped call.
        var activity = new OmnichannelActivity
        {
            ItemId = "act1",
            Channel = "Phone",
            PreferredDestination = "+15551112222",
            InteractionType = ActivityInteractionType.Automated,
            Status = ActivityStatus.InProgress,
        };

        var harness = new Harness(activity, offeredUserId: null);

        // Act
        var result = await harness.Service.RequestHandoffAsync(new OmnichannelHandoffRequest
        {
            Activity = activity,
            TargetQueueId = "queue-1",
            ProviderName = "Telnyx",
            ProviderCallId = "call-abc",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HandoffDisposition.WaitingInQueue, result.Disposition);
        harness.TreatmentService.Verify(
            x => x.RunDueAsync(It.IsAny<ActivityQueue>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ACallerAnAgentTookStraightAway_IsNotPlayedHoldMusicOverTheAgent()
    {
        // Arrange
        // Treatment is for people who are waiting. Starting music for a caller who has just been connected would
        // play it over the agent who answered.
        var activity = new OmnichannelActivity
        {
            ItemId = "act1",
            Channel = "Phone",
            PreferredDestination = "+15551112222",
            InteractionType = ActivityInteractionType.Automated,
            Status = ActivityStatus.InProgress,
        };

        var harness = new Harness(activity, offeredUserId: "u1");

        // Act
        await harness.Service.RequestHandoffAsync(new OmnichannelHandoffRequest
        {
            Activity = activity,
            TargetQueueId = "queue-1",
            ProviderName = "Telnyx",
            ProviderCallId = "call-abc",
        }, TestContext.Current.CancellationToken);

        // Assert
        harness.TreatmentService.Verify(
            x => x.RunDueAsync(It.IsAny<ActivityQueue>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RequestHandoff_CreatesInteraction_SeatsActivity_Enqueues_AndOffers()
    {
        var activity = new OmnichannelActivity
        {
            ItemId = "act1",
            Channel = "Phone",
            PreferredDestination = "+15551112222",
            InteractionType = ActivityInteractionType.Automated,
            Status = ActivityStatus.InProgress,
        };

        var harness = new Harness(activity, offeredUserId: "u1");

        var result = await harness.Service.RequestHandoffAsync(new OmnichannelHandoffRequest
        {
            Activity = activity,
            TargetQueueId = "queue-1",
            ProviderName = "Telnyx",
            ProviderCallId = "call-abc",
            ContactAddress = "+15551112222",
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("u1", result.OfferedToUserId);

        // A Contact Center interaction was created carrying the live provider call so the connect pipeline can bridge.
        Assert.NotNull(harness.CreatedInteraction);
        Assert.Equal(InteractionChannel.Voice, harness.CreatedInteraction.Channel);
        Assert.Equal(InteractionDirection.Inbound, harness.CreatedInteraction.Direction);
        Assert.Equal("act1", harness.CreatedInteraction.ActivityItemId);
        Assert.Equal("Telnyx", harness.CreatedInteraction.ProviderName);
        Assert.Equal("call-abc", harness.CreatedInteraction.ProviderInteractionId);
        Assert.Equal("queue-1", harness.CreatedInteraction.QueueId);

        // The activity moved from the automated lane into the manual/queued lane. Its Source is deliberately
        // untouched: how the call came to exist is a fact escalation does not change.
        Assert.Equal(ActivityInteractionType.Manual, activity.InteractionType);
        Assert.Equal(ActivitySources.Manual, activity.Source);
        Assert.Equal(ActivityKind.Call, activity.Kind);

        harness.QueueService.Verify(q => q.EnqueueAsync("act1", "queue-1", It.IsAny<InteractionPriority?>(), It.IsAny<CancellationToken>()), Times.Once);
        harness.OfferService.Verify(o => o.OfferNextAsync("queue-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RequestHandoff_WhenNoAgentAvailable_StillSucceeds_Waiting()
    {
        var activity = new OmnichannelActivity { ItemId = "act1", InteractionType = ActivityInteractionType.Automated };
        var harness = new Harness(activity, offeredUserId: null);

        var result = await harness.Service.RequestHandoffAsync(new OmnichannelHandoffRequest
        {
            Activity = activity,
            TargetQueueId = "queue-1",
            ProviderName = "Telnyx",
            ProviderCallId = "call-abc",
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Null(result.OfferedToUserId);
        harness.QueueService.Verify(q => q.EnqueueAsync("act1", "queue-1", It.IsAny<InteractionPriority?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RequestHandoff_WhenAlreadyHandedOff_IsIdempotent()
    {
        // A redelivered provider event: the activity is already in the manual lane.
        var activity = new OmnichannelActivity { ItemId = "act1", InteractionType = ActivityInteractionType.Manual };
        var harness = new Harness(activity, offeredUserId: "u1");

        var result = await harness.Service.RequestHandoffAsync(new OmnichannelHandoffRequest
        {
            Activity = activity,
            TargetQueueId = "queue-1",
            ProviderName = "Telnyx",
            ProviderCallId = "call-abc",
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        harness.QueueService.Verify(q => q.EnqueueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<InteractionPriority?>(), It.IsAny<CancellationToken>()), Times.Never);
        harness.OfferService.Verify(o => o.OfferNextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RequestHandoff_AfterHours_SchedulesCallback_AndDoesNotEnqueue()
    {
        var activity = new OmnichannelActivity
        {
            ItemId = "act1",
            InteractionType = ActivityInteractionType.Automated,
            PreferredDestination = "+15551112222",
        };
        var harness = new Harness(activity, offeredUserId: "u1", afterHours: true);

        var result = await harness.Service.RequestHandoffAsync(new OmnichannelHandoffRequest
        {
            Activity = activity,
            TargetQueueId = "queue-1",
            ProviderName = "Telnyx",
            ProviderCallId = "call-abc",
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(HandoffDisposition.CallbackScheduled, result.Disposition);
        // No live routing after hours.
        harness.QueueService.Verify(q => q.EnqueueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<InteractionPriority?>(), It.IsAny<CancellationToken>()), Times.Never);
        harness.CallbackService.Verify(c => c.ScheduleAsync(
            It.Is<CallbackRequest>(r => r.Destination == "+15551112222" && r.QueueId == "queue-1"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RequestHandoff_WhenActivityAlreadyConcluded_IsIdempotent_NoDuplicateCallback()
    {
        // A redelivered speak.ended after an after-hours handoff already concluded the activity: it must not
        // schedule a second callback.
        var activity = new OmnichannelActivity
        {
            ItemId = "act1",
            InteractionType = ActivityInteractionType.Automated,
            Status = ActivityStatus.Completed,
            PreferredDestination = "+15551112222",
        };
        var harness = new Harness(activity, afterHours: true);

        var result = await harness.Service.RequestHandoffAsync(new OmnichannelHandoffRequest
        {
            Activity = activity,
            TargetQueueId = "queue-1",
            ProviderName = "Telnyx",
            ProviderCallId = "call-abc",
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        harness.CallbackService.Verify(c => c.ScheduleAsync(It.IsAny<CallbackRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        harness.QueueService.Verify(q => q.EnqueueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<InteractionPriority?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RequestHandoff_WithoutQueue_Fails()
    {
        var activity = new OmnichannelActivity { ItemId = "act1" };
        var harness = new Harness(activity);

        var result = await harness.Service.RequestHandoffAsync(new OmnichannelHandoffRequest
        {
            Activity = activity,
            TargetQueueId = null,
            ProviderName = "Telnyx",
            ProviderCallId = "call-abc",
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task RequestHandoff_WithoutProviderCall_Fails()
    {
        var activity = new OmnichannelActivity { ItemId = "act1" };
        var harness = new Harness(activity);

        var result = await harness.Service.RequestHandoffAsync(new OmnichannelHandoffRequest
        {
            Activity = activity,
            TargetQueueId = "queue-1",
            ProviderName = "Telnyx",
            ProviderCallId = null,
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
    }


    [Fact]
    public async Task RequestHandoff_PreservesTheOutboundOrigin_OfACallTheDialerPlaced()
    {
        // Arrange
        // Rewriting Source to Inbound made every escalated outbound call look inbound, so campaign reporting
        // credited the dial to inbound traffic and the interaction recorded the wrong direction. The activity's
        // origin is a fact about how the call came to exist; escalating it to a human does not change that.
        var activity = new OmnichannelActivity
        {
            ItemId = "act1",
            Channel = "Phone",
            PreferredDestination = "+16502530000",
            Source = ActivitySources.PreviewDial,
            InteractionType = ActivityInteractionType.Automated,
            Status = ActivityStatus.InProgress,
        };

        var harness = new Harness(activity, offeredUserId: "u1");

        // Act
        var result = await harness.Service.RequestHandoffAsync(new OmnichannelHandoffRequest
        {
            Activity = activity,
            TargetQueueId = "queue-1",
            ProviderName = "Telnyx",
            ProviderCallId = "call-abc",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(ActivitySources.PreviewDial, activity.Source);
        Assert.Equal(ActivityKind.Call, activity.Kind);
        Assert.Equal(ActivityInteractionType.Manual, activity.InteractionType);
        Assert.True(activity.AiEscalated);
        Assert.Equal(InteractionDirection.Outbound, harness.CreatedInteraction.Direction);
    }

    [Fact]
    public async Task RequestHandoff_RecordsAnInboundCallAsInbound()
    {
        // Arrange
        var activity = new OmnichannelActivity
        {
            ItemId = "act1",
            Channel = "Phone",
            PreferredDestination = "+16502530000",
            Source = ActivitySources.Inbound,
            InteractionType = ActivityInteractionType.Automated,
            Status = ActivityStatus.InProgress,
        };

        var harness = new Harness(activity, offeredUserId: "u1");

        // Act
        await harness.Service.RequestHandoffAsync(new OmnichannelHandoffRequest
        {
            Activity = activity,
            TargetQueueId = "queue-1",
            ProviderName = "Telnyx",
            ProviderCallId = "call-abc",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ActivitySources.Inbound, activity.Source);
        Assert.Equal(InteractionDirection.Inbound, harness.CreatedInteraction.Direction);
    }

    [Fact]
    public async Task RequestHandoff_StoresTheContextTheAgentNeedsToPickUpTheConversation()
    {
        // Arrange
        // An agent who answers an escalated call with no idea what the caller has already said starts the
        // conversation over, which is the thing the automated leg was supposed to avoid.
        var activity = new OmnichannelActivity
        {
            ItemId = "act1",
            Channel = "Phone",
            PreferredDestination = "+16502530000",
            InteractionType = ActivityInteractionType.Automated,
            Status = ActivityStatus.InProgress,
        };

        var harness = new Harness(activity, offeredUserId: "u1");

        // Act
        await harness.Service.RequestHandoffAsync(new OmnichannelHandoffRequest
        {
            Activity = activity,
            TargetQueueId = "queue-1",
            ProviderName = "Telnyx",
            ProviderCallId = "call-abc",
            Reason = "customer asked for a person",
            Summary = "Caller wants to change the delivery address on order 4471.",
            AiSessionId = "session-9",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("customer asked for a person", harness.CreatedInteraction.HandoffReason);
        Assert.Equal("Caller wants to change the delivery address on order 4471.", harness.CreatedInteraction.HandoffSummary);
        Assert.Equal("session-9", harness.CreatedInteraction.HandoffAiSessionId);
    }

    [Fact]
    public async Task RequestHandoff_WhenNobodyIsAvailable_ReportsTheCallerIsWaitingInQueue()
    {
        // Arrange
        // "Routed" and "waiting for the next agent" are different things to say to a caller, and the voice
        // handler can only say the right one if the result distinguishes them.
        var activity = new OmnichannelActivity
        {
            ItemId = "act1",
            Channel = "Phone",
            PreferredDestination = "+16502530000",
            InteractionType = ActivityInteractionType.Automated,
            Status = ActivityStatus.InProgress,
        };

        var harness = new Harness(activity, offeredUserId: null);

        // Act
        var result = await harness.Service.RequestHandoffAsync(new OmnichannelHandoffRequest
        {
            Activity = activity,
            TargetQueueId = "queue-1",
            ProviderName = "Telnyx",
            ProviderCallId = "call-abc",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(HandoffDisposition.WaitingInQueue, result.Disposition);
        Assert.Null(result.OfferedToUserId);
    }

    [Fact]
    public async Task RequestHandoff_WhenAnAgentIsAvailable_ReportsRouted()
    {
        // Arrange
        var activity = new OmnichannelActivity
        {
            ItemId = "act1",
            Channel = "Phone",
            PreferredDestination = "+16502530000",
            InteractionType = ActivityInteractionType.Automated,
            Status = ActivityStatus.InProgress,
        };

        var harness = new Harness(activity, offeredUserId: "u1");

        // Act
        var result = await harness.Service.RequestHandoffAsync(new OmnichannelHandoffRequest
        {
            Activity = activity,
            TargetQueueId = "queue-1",
            ProviderName = "Telnyx",
            ProviderCallId = "call-abc",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HandoffDisposition.Routed, result.Disposition);
        Assert.Equal("u1", result.OfferedToUserId);
    }

    [Fact]
    public async Task RequestHandoff_TakesTheInboundCallLock_SoARedeliveredEventCannotEnqueueTwice()
    {
        // Arrange
        // Provider webhooks are at-least-once. Two deliveries of the same escalation arriving together were
        // both passing the idempotency guard, because each read the activity before the other had written it.
        var activity = new OmnichannelActivity
        {
            ItemId = "act1",
            Channel = "Phone",
            PreferredDestination = "+16502530000",
            InteractionType = ActivityInteractionType.Automated,
            Status = ActivityStatus.InProgress,
        };

        var harness = new Harness(activity, offeredUserId: "u1");

        // Act
        await harness.Service.RequestHandoffAsync(new OmnichannelHandoffRequest
        {
            Activity = activity,
            TargetQueueId = "queue-1",
            ProviderName = "Telnyx",
            ProviderCallId = "call-abc",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("ContactCenterInboundVoice:Telnyx:call-abc", harness.DistributedLock.AcquiredKeys);
    }

    [Fact]
    public async Task RequestHandoff_WhenTheSameCallEscalatesTwice_EnqueuesOnce()
    {
        // Arrange
        var activity = new OmnichannelActivity
        {
            ItemId = "act1",
            Channel = "Phone",
            PreferredDestination = "+16502530000",
            InteractionType = ActivityInteractionType.Automated,
            Status = ActivityStatus.InProgress,
        };

        var harness = new Harness(activity, offeredUserId: "u1");

        var request = new OmnichannelHandoffRequest
        {
            Activity = activity,
            TargetQueueId = "queue-1",
            ProviderName = "Telnyx",
            ProviderCallId = "call-abc",
        };

        // Act
        await harness.Service.RequestHandoffAsync(request, TestContext.Current.CancellationToken);
        var second = await harness.Service.RequestHandoffAsync(request, TestContext.Current.CancellationToken);

        // Assert
        // The second delivery finds the activity already in the manual lane and reports success without acting,
        // so the caller is not seated in the queue a second time.
        Assert.True(second.Succeeded);
        harness.QueueService.Verify(
            queueService => queueService.EnqueueAsync("act1", "queue-1", It.IsAny<InteractionPriority?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RequestHandoff_AfterHours_SchedulesExactlyOneCallback_EvenWhenTheEventIsRedelivered()
    {
        // Arrange
        var activity = new OmnichannelActivity
        {
            ItemId = "act1",
            Channel = "Phone",
            PreferredDestination = "+16502530000",
            InteractionType = ActivityInteractionType.Automated,
            Status = ActivityStatus.InProgress,
        };

        var harness = new Harness(activity, afterHours: true);

        var request = new OmnichannelHandoffRequest
        {
            Activity = activity,
            TargetQueueId = "queue-1",
            ProviderName = "Telnyx",
            ProviderCallId = "call-abc",
        };

        // Act
        var first = await harness.Service.RequestHandoffAsync(request, TestContext.Current.CancellationToken);
        await harness.Service.RequestHandoffAsync(request, TestContext.Current.CancellationToken);

        // Assert
        // The first call concludes the activity, so the second finds it terminal and does nothing. A caller who
        // gets two callbacks for one after-hours call is a caller who gets phoned twice.
        Assert.Equal(HandoffDisposition.CallbackScheduled, first.Disposition);
        harness.CallbackService.Verify(
            callbackService => callbackService.ScheduleAsync(It.IsAny<CallbackRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private sealed class Harness
    {
        public Mock<IActivityQueueService> QueueService { get; } = new();

        public Mock<IVoiceQueueOfferService> OfferService { get; } = new();

        public Mock<IQueueTreatmentService> TreatmentService { get; } = new();

        public Interaction CreatedInteraction { get; private set; }

        public Mock<ICallbackService> CallbackService { get; } = new();

        public FakeDistributedLock DistributedLock { get; } = new();

        public VoiceAgentHandoffService Service { get; }

        public Harness(OmnichannelActivity activity = null, string offeredUserId = null, bool afterHours = false)
        {
            var activityManager = new Mock<IOmnichannelActivityManager>();
            activityManager.Setup(m => m.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(activity);
            activityManager.Setup(m => m.UpdateAsync(It.IsAny<OmnichannelActivity>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
                .Returns(ValueTask.CompletedTask);

            var queueManager = new Mock<IActivityQueueManager>();
            queueManager.Setup(m => m.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ActivityQueue { ItemId = "queue-1", BusinessHoursCalendarId = afterHours ? "cal-1" : null });

            var businessHoursGate = new Mock<IBusinessHoursGate>();
            businessHoursGate.Setup(g => g.IsOpenAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(!afterHours);

            CallbackService.Setup(c => c.ScheduleAsync(It.IsAny<CallbackRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CallbackRequest r, CancellationToken _) => r);

            var clock = new Mock<IClock>();
            clock.SetupGet(c => c.UtcNow).Returns(DateTime.UtcNow);

            var interactionManager = new Mock<IInteractionManager>();
            interactionManager.Setup(m => m.FindByActivityIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Interaction)null);
            interactionManager.Setup(m => m.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Interaction { ItemId = "int1" });
            interactionManager.Setup(m => m.CreateAsync(It.IsAny<Interaction>(), It.IsAny<CancellationToken>()))
                .Returns(ValueTask.CompletedTask)
                .Callback<Interaction, CancellationToken>((i, _) => CreatedInteraction = i);
            interactionManager.Setup(m => m.UpdateAsync(It.IsAny<Interaction>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
                .Returns(ValueTask.CompletedTask);

            QueueService.Setup(q => q.EnqueueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<InteractionPriority?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new QueueItem { ItemId = "qi1" });

            OfferService.Setup(o => o.OfferNextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(offeredUserId);

            Service = new VoiceAgentHandoffService(
                interactionManager.Object,
                activityManager.Object,
                new FakeContactCenterWorkStateService(),
                QueueService.Object,
                OfferService.Object,
                queueManager.Object,
                clock.Object,
                businessHoursGate.Object,
                CallbackService.Object,
                DistributedLock,
                new OptionsWrapper<ContactCenterCoordinationOptions>(new ContactCenterCoordinationOptions()),
                TreatmentService.Object,
                new Mock<ISession>().Object,
                NullLogger<VoiceAgentHandoffService>.Instance);
        }
    }
}

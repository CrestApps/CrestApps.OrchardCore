using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Modules;

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

        // The activity moved from the automated lane into the manual/queued lane.
        Assert.Equal(ActivityInteractionType.Manual, activity.InteractionType);
        Assert.Equal(ActivitySources.Inbound, activity.Source);
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

    private sealed class Harness
    {
        public Mock<IActivityQueueService> QueueService { get; } = new();

        public Mock<IVoiceQueueOfferService> OfferService { get; } = new();

        public Interaction CreatedInteraction { get; private set; }

        public Mock<ICallbackService> CallbackService { get; } = new();

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

            // The gate and callback service are resolved optionally from the provider, mirroring production.
            var services = new ServiceCollection();
            services.AddSingleton(businessHoursGate.Object);
            services.AddSingleton(CallbackService.Object);

            Service = new VoiceAgentHandoffService(
                interactionManager.Object,
                activityManager.Object,
                new FakeContactCenterWorkStateService(),
                QueueService.Object,
                OfferService.Object,
                queueManager.Object,
                clock.Object,
                services.BuildServiceProvider(),
                NullLogger<VoiceAgentHandoffService>.Instance);
        }
    }
}

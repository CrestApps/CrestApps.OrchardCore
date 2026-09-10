using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Taking a waiting caller out of their queue and sending them to voicemail.
/// </summary>
/// <remarks>
/// This is one moment from the caller's side: the hold music stops and the greeting starts. It is two mechanisms
/// underneath — the dequeue stops the music through a direct provider call, while the greeting is a provider
/// command whose dispatch is scheduled for after the scope commits, because the command row has to be committed
/// before another scope can pick it up. Both live calls that went wrong here went wrong in the seam between them:
/// first the greeting never played at all, then it played twenty seconds after the music stopped. These tests pin
/// the pairing so the two halves cannot drift apart again.
/// </remarks>
public sealed class InboundVoiceVoicemailDispatchTests
{
    private const string ActivityId = "activity-1";
    private const string InteractionId = "interaction-1";
    private const string ProviderCallId = "v3:call-1";

    [Fact]
    public async Task AWaitingCaller_IsBothTakenOutOfTheQueueAndSentAGreeting()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var moved = await harness.Processor.SendWaitingToVoicemailAsync(
            ActivityId,
            ContactCenterConstants.QueueLimits.MaxWaitVoicemailReasonCode,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(moved);

        // The dequeue is what stops the hold music. On its own it leaves the caller in silence.
        harness.QueueService.Verify(
            service => service.DequeueAsync(harness.QueueItem, QueueItemStatus.Removed, It.IsAny<CancellationToken>()),
            Times.Once);

        // The command is what eventually greets them. On its own the music would still be playing over it.
        Assert.NotNull(harness.RegisteredCommand);
        Assert.Equal(ProviderCommandType.SendToVoicemail, harness.RegisteredCommand.CommandType);
        Assert.Equal(ActivityId, harness.RegisteredCommand.ActivityItemId);
        Assert.Equal(InteractionId, harness.RegisteredCommand.InteractionId);
    }

    [Fact]
    public async Task TheGreeting_IsDispatchedAfterTheCommandRowIsCommitted_NotDuringTheSameUnitOfWork()
    {
        // Arrange
        // Dispatching inline would hand a fresh scope a command id that is not committed yet, and the processor
        // would not find the row. This is the ordering the whole path depends on, so it is asserted rather than
        // assumed.
        var harness = new Harness();

        // Act
        await harness.Processor.SendWaitingToVoicemailAsync(
            ActivityId,
            ContactCenterConstants.QueueLimits.MaxWaitVoicemailReasonCode,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(harness.ScheduledAfterCommit, "The voicemail dispatch was not scheduled for after the scope committed.");
        Assert.Equal(0, harness.DispatchesRun);

        // Act
        // Standing in for the scope committing.
        await harness.RunScheduledWorkAsync();

        // Assert
        Assert.Equal(1, harness.DispatchesRun);
        Assert.Equal(harness.RegisteredCommand.CommandId, harness.DispatchedCommandId);
    }

    [Fact]
    public async Task ACallerWhoIsNoLongerWaiting_IsLeftAlone()
    {
        // Arrange
        // Once the caller has been reserved for an agent, the reservation owns the call. Sending them to voicemail
        // from under it would stop the music on a call an agent is about to be connected to.
        var harness = new Harness();
        harness.QueueItem.TransitionTo(QueueItemStatus.Reserved);

        // Act
        var moved = await harness.Processor.SendWaitingToVoicemailAsync(
            ActivityId,
            ContactCenterConstants.QueueLimits.MaxWaitVoicemailReasonCode,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(moved);
        Assert.Null(harness.RegisteredCommand);
        harness.QueueService.Verify(
            service => service.DequeueAsync(It.IsAny<QueueItem>(), It.IsAny<QueueItemStatus>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AQueuedConversationThatIsNotACall_IsNotSentToVoicemail()
    {
        // Arrange
        // An SMS or chat thread has no call to move. Completing its activity would silently drop a conversation
        // somebody is still waiting on a reply to.
        var harness = new Harness(channel: InteractionChannel.Sms);

        // Act
        var moved = await harness.Processor.SendWaitingToVoicemailAsync(
            ActivityId,
            ContactCenterConstants.QueueLimits.MaxWaitVoicemailReasonCode,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(moved);
        Assert.Null(harness.RegisteredCommand);
        harness.QueueService.Verify(
            service => service.DequeueAsync(It.IsAny<QueueItem>(), It.IsAny<QueueItemStatus>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ACallTheProviderNeverIdentified_EndsTheInteractionInsteadOfWaitingOnAGreeting()
    {
        // Arrange
        // With no provider call id there is nothing to play a greeting into, so there is no command to wait on
        // and the interaction has to be closed here rather than left open for a dispatch that never comes.
        var harness = new Harness(providerCallId: null);

        // Act
        var moved = await harness.Processor.SendWaitingToVoicemailAsync(
            ActivityId,
            ContactCenterConstants.QueueLimits.MaxWaitVoicemailReasonCode,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(moved);
        Assert.Null(harness.RegisteredCommand);
        Assert.False(harness.ScheduledAfterCommit);
        Assert.Equal(InteractionStatus.Ended, harness.Interaction.Status);
        Assert.NotNull(harness.Interaction.EndedUtc);
    }

    /// <summary>
    /// The collaborators the voicemail path touches, with the rest of the processor's dependencies stubbed.
    /// </summary>
    private sealed class Harness
    {
        private readonly List<Func<Task>> _scheduled = [];

        public Harness(InteractionChannel channel = InteractionChannel.Voice, string providerCallId = ProviderCallId)
        {
            QueueItem = new QueueItem
            {
                ItemId = "queue-item-1",
                ActivityItemId = ActivityId,
                QueueId = "queue-1",
            };

            Interaction = new Interaction
            {
                ItemId = InteractionId,
                Channel = channel,
                ProviderName = providerCallId is null ? null : "Telnyx",
                ProviderInteractionId = providerCallId,
            };

            var activity = new OmnichannelActivity { ItemId = ActivityId };

            var queueItemManager = new Mock<IQueueItemManager>();
            queueItemManager
                .Setup(manager => manager.FindByActivityIdAsync(ActivityId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => QueueItem);

            var activityManager = new Mock<IOmnichannelActivityManager>();
            activityManager
                .Setup(manager => manager.FindByIdAsync(ActivityId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(activity);

            var interactionManager = new Mock<IInteractionManager>();
            interactionManager
                .Setup(manager => manager.FindByActivityIdAsync(ActivityId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Interaction);

            var commandStateService = new Mock<IProviderCommandStateService>();
            commandStateService
                .Setup(service => service.RegisterAsync(It.IsAny<ProviderCommandRegistration>(), It.IsAny<CancellationToken>()))
                .Callback<ProviderCommandRegistration, CancellationToken>((registration, _) => RegisteredCommand = registration)
                .ReturnsAsync(new ProviderCommand());

            var commandProcessor = new Mock<IProviderCommandProcessor>();
            commandProcessor
                .Setup(processor => processor.DispatchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, CancellationToken>((commandId, _) =>
                {
                    DispatchesRun++;
                    DispatchedCommandId = commandId;
                })
                .ReturnsAsync(new ProviderCommand());

            var scopeExecutor = new Mock<IContactCenterScopeExecutor>();
            scopeExecutor
                .Setup(executor => executor.ScheduleAfterCommit(It.IsAny<Func<IProviderCommandProcessor, Task>>()))
                .Callback<Func<IProviderCommandProcessor, Task>>(operation =>
                {
                    ScheduledAfterCommit = true;
                    _scheduled.Add(() => operation(commandProcessor.Object));
                })
                .Returns(true);

            var clock = new Mock<IClock>();
            clock.SetupGet(c => c.UtcNow).Returns(new DateTime(2026, 9, 6, 21, 34, 8, DateTimeKind.Utc));

            QueueService = new Mock<IActivityQueueService>();

            Processor = new InboundVoiceCallProcessor(
                new Mock<IOmnichannelChannelEndpointManager>().Object,
                new Mock<ISubjectFlowSettingsService>().Object,
                activityManager.Object,
                new Mock<IContactCenterWorkStateService>().Object,
                new Mock<IContactCenterActivityWriter>().Object,
                new Mock<IContentManager>().Object,
                interactionManager.Object,
                new Mock<IActivityQueueManager>().Object,
                queueItemManager.Object,
                QueueService.Object,
                new Mock<IQueueLimitService>().Object,
                new Mock<IInboundContactLookup>().Object,
                new EntryPointResolverChain([]),
                commandStateService.Object,
                new Mock<IVoiceQueueOfferService>().Object,
                new Mock<IDistributedLock>().Object,
                scopeExecutor.Object,
                new Mock<IContactCenterFeatureWorkManager>().Object,
                clock.Object,
                Options.Create(new ContactCenterCoordinationOptions()));
        }

        public InboundVoiceCallProcessor Processor { get; }

        public Mock<IActivityQueueService> QueueService { get; }

        public QueueItem QueueItem { get; }

        public Interaction Interaction { get; }

        public ProviderCommandRegistration RegisteredCommand { get; private set; }

        public bool ScheduledAfterCommit { get; private set; }

        public int DispatchesRun { get; private set; }

        public string DispatchedCommandId { get; private set; }

        public async Task RunScheduledWorkAsync()
        {
            foreach (var work in _scheduled)
            {
                await work();
            }

            _scheduled.Clear();
        }
    }
}

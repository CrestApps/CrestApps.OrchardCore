using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Tests.Modules.ContactCenter.Integration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Putting a caller through to what they chose on an entry point's phone menu. Only a queue choice used to do
/// anything, and that one only enqueued the caller without offering them to an agent; every other choice was saved,
/// validated, and then left the caller on the line.
/// </summary>
public sealed class IvrCallRouterTests
{
    [Fact]
    public async Task AQueueChoice_EnqueuesTheCaller_OffersThemAndPlaysHoldMusic()
    {
        // Arrange
        var harness = new RouterHarness();
        harness.OfferNextTo = "user-1";

        // Act
        await harness.RouteAsync(new IvrStep(IvrStepKind.RouteToQueue, "root", null, null, "queue-support"));

        // Assert
        Assert.Equal(("activity-1", "queue-support", InteractionPriority.High), harness.Enqueued.Single());
        Assert.Equal(["queue-support"], harness.OfferedQueues);
        Assert.Equal("queue-support", harness.Interaction.QueueId);
        Assert.Equal(("queue-support", "call-1"), harness.WaitingAudio.Single());
        Assert.Empty(harness.Treatments);
    }

    [Fact]
    public async Task AQueueChoiceThatPlaysNothing_GivesTheCallerARingingToneWhileTheyWait()
    {
        // Arrange
        // The menu answered the caller, so the network stopped ringing them. A queue with no treatment at all left
        // them in dead silence until an agent picked up, which sounds exactly like a dropped call.
        var harness = new RouterHarness();

        // Act
        await harness.RouteAsync(new IvrStep(IvrStepKind.RouteToQueue, "root", null, null, "queue-support"));

        // Assert
        Assert.Equal(["queue-support"], harness.Treatments);
        Assert.Equal(("queue-support", "call-1"), harness.WaitingAudio.Single());
    }

    [Fact]
    public async Task AQueueChoiceWithItsOwnTreatment_LeavesTheCallerToIt()
    {
        // Arrange
        var harness = new RouterHarness();
        harness.Queues["queue-support"].Treatment = new QueueTreatmentSettings { HoldMusicMediaId = "media-1" };

        // Act
        await harness.RouteAsync(new IvrStep(IvrStepKind.RouteToQueue, "root", null, null, "queue-support"));

        // Assert
        Assert.Equal(["queue-support"], harness.Treatments);
        Assert.Empty(harness.WaitingAudio);
    }

    [Fact]
    public async Task AnAgentChoice_PlaysTheLinesQueueMusicWhileTheAgentRings()
    {
        // Arrange
        // Caller silence while a chosen agent rang: the menu had answered them, so no ringback, and nothing was played.
        var harness = new RouterHarness();

        // Act
        await harness.RouteAsync(new IvrStep(IvrStepKind.RouteToAgent, "root", null, null, "agent-7"));

        // Assert
        Assert.Equal(("queue-main", "call-1"), harness.WaitingAudio.Single());
    }

    [Fact]
    public async Task AnAgentChoiceOnAPersonalLine_PlaysARingingToneWhileTheAgentRings()
    {
        // Arrange
        var harness = new RouterHarness();
        harness.EntryPoint.TargetType = EntryPointTargetType.Agent;
        harness.EntryPoint.TargetAgentId = "agent-owner";
        harness.EntryPoint.TargetQueueId = null;

        // Act
        await harness.RouteAsync(new IvrStep(IvrStepKind.RouteToAgent, "root", null, null, "agent-7"));

        // Assert
        Assert.Equal((null, "call-1"), harness.WaitingAudio.Single());
    }

    [Fact]
    public async Task AQueueChoiceWithNobodyFree_StartsTheQueuesTreatmentNow()
    {
        // Arrange
        // The caller was answered to hear the menu. Left to the next sweep, they sat in silence, which sounds like a
        // dropped call.
        var harness = new RouterHarness();

        // Act
        await harness.RouteAsync(new IvrStep(IvrStepKind.RouteToQueue, "root", null, null, "queue-support"));

        // Assert
        Assert.Equal(["queue-support"], harness.Treatments);
        Assert.DoesNotContain(harness.WaitingAudio, entry => entry.QueueId == "queue-main");
    }

    [Fact]
    public async Task AFullQueue_OverflowsLikeAnyOtherCaller()
    {
        // Arrange
        var harness = new RouterHarness();
        harness.Admission = QueueAdmissionDecision.Overflow("queue-overflow");

        // Act
        await harness.RouteAsync(new IvrStep(IvrStepKind.RouteToQueue, "root", null, null, "queue-support"));

        // Assert
        Assert.Equal("queue-overflow", harness.Enqueued.Single().QueueId);
        Assert.Equal("queue-overflow", harness.Interaction.QueueId);
    }

    [Fact]
    public async Task AFullQueueThatSendsCallersToVoicemail_DoesSo()
    {
        // Arrange
        var harness = new RouterHarness();
        harness.Admission = QueueAdmissionDecision.SendToVoicemail();

        // Act
        await harness.RouteAsync(new IvrStep(IvrStepKind.RouteToQueue, "root", null, null, "queue-support"));

        // Assert
        Assert.Empty(harness.Enqueued);
        Assert.Equal(("activity-1", ContactCenterConstants.QueueLimits.QueueFullVoicemailReasonCode), harness.Voicemails.Single());
    }

    [Fact]
    public async Task AQueueThatNoLongerExists_SendsTheCallerToTheEntryPointsTarget()
    {
        // Arrange
        // A menu edited after a queue was deleted must still put the caller through to somebody.
        var harness = new RouterHarness();

        // Act
        await harness.RouteAsync(new IvrStep(IvrStepKind.RouteToQueue, "root", null, null, "queue-deleted"));

        // Assert
        Assert.Equal("queue-main", harness.Enqueued.Single().QueueId);
        Assert.Contains(harness.Audit, entry => entry.EventType == ContactCenterConstants.Events.IvrFallbackTaken && entry.Data.Reason == "QueueUnavailable");
    }

    [Fact]
    public async Task WhenNeitherTheChoiceNorTheTargetCanBeReached_TheCallerGoesToVoicemail()
    {
        // Arrange
        var harness = new RouterHarness();
        harness.EntryPoint.TargetQueueId = "queue-deleted";

        // Act
        await harness.RouteAsync(new IvrStep(IvrStepKind.RouteToQueue, "root", null, null, "queue-also-deleted"));

        // Assert
        Assert.Empty(harness.Enqueued);
        Assert.Equal(("activity-1", IvrCallRouter.UnroutableVoicemailReasonCode), harness.Voicemails.Single());
    }

    [Fact]
    public async Task AnAgentChoice_RingsThatAgentLikeAPersonalLine()
    {
        // Arrange
        // Carried under the direct-routing queue and tagged for the agent, the call is held and re-offered to that
        // agent when they become available, and goes to voicemail once the entry point's ring window runs out.
        var harness = new RouterHarness();
        harness.EntryPoint.RingTimeoutSeconds = 45;

        // Act
        await harness.RouteAsync(new IvrStep(IvrStepKind.RouteToAgent, "root", null, null, "agent-7"));

        // Assert
        Assert.Equal(("activity-1", ContactCenterConstants.DirectRouting.QueueId, InteractionPriority.High), harness.Enqueued.Single());
        Assert.Equal(("activity-1", "agent-7", 45), harness.DirectOffers.Single());
        Assert.Equal(ContactCenterConstants.DirectRouting.QueueId, harness.Interaction.QueueId);
        Assert.Equal("agent-7", harness.Interaction.TechnicalMetadata[ContactCenterConstants.DirectRouting.TargetAgentMetadataKey]);
        Assert.Equal(45, harness.Interaction.TechnicalMetadata[ContactCenterConstants.DirectRouting.RingTimeoutMetadataKey]);
        Assert.Empty(harness.OfferedQueues);
    }

    [Fact]
    public async Task AnAgentChoiceWithVoicemailOff_HoldsTheCallerForTheAgent()
    {
        // Arrange
        var harness = new RouterHarness();
        harness.EntryPoint.VoicemailEnabled = false;

        // Act
        await harness.RouteAsync(new IvrStep(IvrStepKind.RouteToAgent, "root", null, null, "agent-7"));

        // Assert
        Assert.Equal(0, harness.DirectOffers.Single().RingTimeoutSeconds);
    }

    [Fact]
    public async Task AVoicemailChoice_SendsTheCallerToVoicemail()
    {
        // Arrange
        var harness = new RouterHarness();

        // Act
        await harness.RouteAsync(new IvrStep(IvrStepKind.Voicemail, "root", null, null, null));

        // Assert
        Assert.Equal(("activity-1", IvrCallRouter.VoicemailReasonCode), harness.Voicemails.Single());
        Assert.Empty(harness.Enqueued);
    }

    [Fact]
    public async Task AVoicemailChoiceOnAPersonalLine_LeavesTheMessageForItsAgent()
    {
        // Arrange
        var harness = new RouterHarness();
        harness.EntryPoint.TargetType = EntryPointTargetType.Agent;
        harness.EntryPoint.TargetAgentId = "agent-owner";

        // Act
        await harness.RouteAsync(new IvrStep(IvrStepKind.Voicemail, "root", null, null, null));

        // Assert
        Assert.Equal("agent-owner", harness.Interaction.TechnicalMetadata[ContactCenterConstants.DirectRouting.TargetAgentMetadataKey]);
        Assert.Single(harness.Voicemails);
    }

    [Fact]
    public async Task AVoicemailChoiceOnAQueueLine_LeavesTheMessageInTheLinesVoicemailInbox()
    {
        // Arrange
        // A queue line has no agent of its own, so the message was recorded and delivered to nobody.
        var harness = new RouterHarness();
        harness.EntryPoint.VoicemailRecipientAgentId = "agent-supervisor";

        // Act
        await harness.RouteAsync(new IvrStep(IvrStepKind.Voicemail, "root", null, null, null));

        // Assert
        Assert.Equal("agent-supervisor", harness.Interaction.TechnicalMetadata[ContactCenterConstants.Voicemail.MailboxAgentMetadataKey]);
        Assert.False(harness.Interaction.TechnicalMetadata.ContainsKey(ContactCenterConstants.DirectRouting.TargetAgentMetadataKey));
        Assert.Single(harness.Voicemails);
    }

    [Fact]
    public async Task AVoicemailChoiceOnALineDeliveringToTheSharedBox_LeavesTheMessageForTheQueue()
    {
        // Arrange
        var harness = new RouterHarness();
        harness.EntryPoint.VoicemailDestination = EntryPointVoicemailDestination.QueueSharedBox;
        harness.EntryPoint.VoicemailRecipientAgentId = "agent-supervisor";

        // Act
        await harness.RouteAsync(new IvrStep(IvrStepKind.Voicemail, "root", null, null, null));

        // Assert
        Assert.Equal("queue-main", harness.Interaction.TechnicalMetadata[ContactCenterConstants.Voicemail.SharedMailboxQueueMetadataKey]);
        Assert.False(harness.Interaction.TechnicalMetadata.ContainsKey(ContactCenterConstants.Voicemail.MailboxAgentMetadataKey));
        Assert.Single(harness.Voicemails);
    }

    [Fact]
    public async Task AFailedExternalTransfer_TakesTheMenusFallback()
    {
        // Arrange
        // The destination was busy or did not answer. The caller was still on the line, and was dropped.
        var harness = new RouterHarness();
        harness.EntryPoint.IvrFlow.FallbackAction = new IvrAction { Kind = IvrActionKind.RouteToQueue, TargetId = "queue-support" };

        // Act
        await harness.Router.RecoverFailedTransferAsync("interaction-1", "dest-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("queue-support", harness.Enqueued.Single().QueueId);
        Assert.Empty(harness.ExternalTransfers);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(IvrActionKind.ExternalTransfer, "dest-1")]
    [InlineData(IvrActionKind.SubMenu, "root")]
    [InlineData(IvrActionKind.Repeat, null)]
    public async Task AFailedExternalTransfer_WithNoUsableFallback_GoesToTheEntryPointsTarget(IvrActionKind? kind, string targetId)
    {
        // Arrange
        // No fallback, a fallback that would ring the same number again, or one that would replay a menu the caller
        // has already chosen from: the caller goes to the line's own target instead.
        var harness = new RouterHarness();
        harness.EntryPoint.IvrFlow.FallbackAction = kind is null ? null : new IvrAction { Kind = kind.Value, TargetId = targetId };

        // Act
        await harness.Router.RecoverFailedTransferAsync("interaction-1", "dest-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("queue-main", harness.Enqueued.Single().QueueId);
        Assert.Empty(harness.ExternalTransfers);
    }

    [Fact]
    public async Task AFailedExternalTransfer_WhoseFallbackIsAnotherNumber_RingsThatNumber()
    {
        // Arrange
        var harness = new RouterHarness();
        harness.EntryPoint.IvrFlow.FallbackAction = new IvrAction { Kind = IvrActionKind.ExternalTransfer, TargetId = "dest-2" };

        // Act
        await harness.Router.RecoverFailedTransferAsync("interaction-1", "dest-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["dest-2"], harness.ExternalTransfers);
    }

    [Fact]
    public async Task AFailedExternalTransfer_ForACallerWhoHasGone_RoutesNothing()
    {
        // Arrange
        var harness = new RouterHarness();
        harness.Interaction.TransitionTo(InteractionStatus.Ended);

        // Act
        await harness.Router.RecoverFailedTransferAsync("interaction-1", "dest-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(harness.Enqueued);
        Assert.Empty(harness.Voicemails);
    }

    [Fact]
    public async Task AnExternalChoice_IsTransferred()
    {
        // Arrange
        var harness = new RouterHarness();

        // Act
        await harness.RouteAsync(new IvrStep(IvrStepKind.ExternalTransfer, "root", null, null, "dest-1"));

        // Assert
        Assert.Equal("dest-1", harness.ExternalTransfers.Single());
        Assert.Empty(harness.Enqueued);
    }

    [Fact]
    public async Task AnExternalChoiceThatCannotBeMade_SendsTheCallerToTheEntryPointsTarget()
    {
        // Arrange
        // A destination disabled after the menu was built, or a refused transfer, leaves the caller on the line.
        var harness = new RouterHarness();
        harness.ExternalTransferSucceeds = false;

        // Act
        await harness.RouteAsync(new IvrStep(IvrStepKind.ExternalTransfer, "root", null, null, "dest-1"));

        // Assert
        Assert.Equal("queue-main", harness.Enqueued.Single().QueueId);
        Assert.Contains(harness.Audit, entry => entry.EventType == ContactCenterConstants.Events.IvrFallbackTaken && entry.Data.Reason == "ExternalTransferUnavailable");
    }

    [Fact]
    public async Task NoFallback_SendsTheCallerToTheEntryPointsQueue()
    {
        // Arrange
        var harness = new RouterHarness();

        // Act
        await harness.RouteAsync(IvrStep.Done with { IsFallback = true });

        // Assert
        Assert.Equal("queue-main", harness.Enqueued.Single().QueueId);
    }

    [Fact]
    public async Task NoFallbackOnAPersonalLine_RingsItsAgent()
    {
        // Arrange
        var harness = new RouterHarness();
        harness.EntryPoint.TargetType = EntryPointTargetType.Agent;
        harness.EntryPoint.TargetAgentId = "agent-owner";

        // Act
        await harness.RouteAsync(IvrStep.Done);

        // Assert
        Assert.Equal("agent-owner", harness.DirectOffers.Single().AgentId);
    }

    [Theory]
    [InlineData(IvrStepKind.Prompt)]
    [InlineData(IvrStepKind.Ignored)]
    public async Task AMenuStep_RoutesNowhere(IvrStepKind kind)
    {
        // Arrange
        var harness = new RouterHarness();

        // Act
        await harness.RouteAsync(new IvrStep(kind, "root", "Press 1.", null, null));

        // Assert
        Assert.Empty(harness.Enqueued);
        Assert.Empty(harness.Voicemails);
        Assert.Empty(harness.DirectOffers);
    }

    [Fact]
    public async Task ACallerWhoHasAlreadyHungUp_IsNotRouted()
    {
        // Arrange
        var harness = new RouterHarness();
        harness.Interaction.TransitionTo(InteractionStatus.Ended);

        // Act
        await harness.RouteAsync(new IvrStep(IvrStepKind.RouteToQueue, "root", null, null, "queue-support"));

        // Assert
        Assert.Empty(harness.Enqueued);
    }

    [Fact]
    public async Task StartingAMenuThatCannotBePlayed_PutsTheCallerThroughToTheTarget()
    {
        // Arrange
        var harness = new RouterHarness();
        harness.StartStep = IvrStep.Done with { IsFallback = true };

        // Act
        await harness.Router.StartAsync("interaction-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("queue-main", harness.Enqueued.Single().QueueId);
    }

    [Fact]
    public async Task StartingAMenu_WaitsForTheCallersChoice()
    {
        // Arrange
        var harness = new RouterHarness();
        harness.StartStep = new IvrStep(IvrStepKind.Prompt, "root", "Press 1.", null, null);

        // Act
        await harness.Router.StartAsync("interaction-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(harness.Enqueued);
        Assert.Equal(1, harness.Starts);
    }

    private sealed class RouterHarness
    {
        public Dictionary<string, ActivityQueue> Queues { get; } = new(StringComparer.Ordinal)
        {
            ["queue-main"] = new ActivityQueue { ItemId = "queue-main", Name = "Main", Enabled = true },
            ["queue-support"] = new ActivityQueue { ItemId = "queue-support", Name = "Support", Enabled = true },
            ["queue-overflow"] = new ActivityQueue { ItemId = "queue-overflow", Name = "Overflow", Enabled = true },
        };

        public RouterHarness()
        {
            Interaction = new Interaction
            {
                ItemId = "interaction-1",
                ActivityItemId = "activity-1",
                ProviderName = "Telnyx",
                ProviderInteractionId = "call-1",
                Channel = InteractionChannel.Voice,
                Direction = InteractionDirection.Inbound,
            };

            EntryPoint = new ContactCenterEntryPoint
            {
                ItemId = "entry-1",
                TargetType = EntryPointTargetType.Queue,
                TargetQueueId = "queue-main",
                Priority = InteractionPriority.High,
                IvrFlow = new IvrFlow { RootNodeId = "root", Nodes = [new IvrNode { NodeId = "root", Prompt = "Press 1." }] },
            };

            var interactions = new Mock<IInteractionManager>();
            interactions.Setup(x => x.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => Interaction);

            var resolver = new Mock<IEntryPointFlowResolver>();
            resolver.Setup(x => x.FindEntryPointAsync(It.IsAny<Interaction>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => EntryPoint);

            var ivr = new Mock<IIvrExecutionService>();
            ivr.Setup(x => x.StartAsync(It.IsAny<Interaction>(), It.IsAny<IvrFlow>(), It.IsAny<CancellationToken>()))
                .Callback(() => Starts++)
                .ReturnsAsync(() => StartStep);

            var queueManager = new Mock<IActivityQueueManager>();
            queueManager.Setup(x => x.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns<string, CancellationToken>((id, _) => ValueTask.FromResult(id is not null && Queues.TryGetValue(id, out var queue) ? queue : null));

            var limits = new Mock<IQueueLimitService>();
            limits.Setup(x => x.AdmitAsync(It.IsAny<ActivityQueue>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ActivityQueue queue, CancellationToken _) => Admission ?? QueueAdmissionDecision.Admit(queue.ItemId));

            var queueService = new Mock<IActivityQueueService>();
            queueService.Setup(x => x.EnqueueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<InteractionPriority?>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, InteractionPriority?, CancellationToken>((activityId, queueId, priority, _) => Enqueued.Add((activityId, queueId, priority)))
                .ReturnsAsync(new QueueItem());

            var offers = new Mock<IVoiceQueueOfferService>();
            offers.Setup(x => x.OfferNextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, CancellationToken>((queueId, _) => OfferedQueues.Add(queueId))
                .ReturnsAsync(() => OfferNextTo);
            offers.Setup(x => x.OfferToAgentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, string, int?, CancellationToken>((activityId, _, agentId, ring, _) => DirectOffers.Add((activityId, agentId, ring)))
                .ReturnsAsync((string)null);

            var treatment = new Mock<IQueueTreatmentService>();
            treatment.Setup(x => x.RunDueAsync(It.IsAny<ActivityQueue>(), It.IsAny<CancellationToken>()))
                .Callback<ActivityQueue, CancellationToken>((queue, _) => Treatments.Add(queue.ItemId))
                .ReturnsAsync(1);
            treatment.Setup(x => x.StartWaitingAudioAsync(It.IsAny<ActivityQueue>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<ActivityQueue, string, CancellationToken>((queue, callId, _) => WaitingAudio.Add((queue?.ItemId, callId)))
                .Returns(Task.CompletedTask);

            var agents = new Mock<IAgentProfileManager>();
            agents.Setup(x => x.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns<string, CancellationToken>((id, _) => ValueTask.FromResult(id is "agent-7" or "agent-owner" ? new AgentProfile { ItemId = id } : null));

            var processor = new Mock<IInboundVoiceCallProcessor>();
            processor.Setup(x => x.SendToVoicemailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, CancellationToken>((activityId, reason, _) => Voicemails.Add((activityId, reason)))
                .ReturnsAsync(true);

            var external = new Mock<IIvrExternalTransferService>();
            external.Setup(x => x.TransferAsync(It.IsAny<Interaction>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<Interaction, string, CancellationToken>((_, destination, _) => ExternalTransfers.Add(destination))
                .ReturnsAsync(() => ExternalTransferSucceeds);

            var audit = new Mock<IContactCenterAuditRecorder>();
            audit.Setup(x => x.RecordCallAsync(
                    It.IsAny<string>(),
                    It.IsAny<CallLifecycleEventData>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<ContactCenterActor>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, CallLifecycleEventData, DateTime, ContactCenterActor, string, CancellationToken>((eventType, data, _, _, _, _) => Audit.Add((eventType, data)))
                .Returns(Task.CompletedTask);

            Router = new IvrCallRouter(
                interactions.Object,
                resolver.Object,
                ivr.Object,
                queueManager.Object,
                limits.Object,
                queueService.Object,
                offers.Object,
                treatment.Object,
                agents.Object,
                processor.Object,
                external.Object,
                audit.Object,
                new Mock<global::YesSql.ISession>().Object,
                new TestClock(),
                NullLogger<IvrCallRouter>.Instance);
        }

        public Interaction Interaction { get; }

        public ContactCenterEntryPoint EntryPoint { get; }

        public IvrCallRouter Router { get; }

        public QueueAdmissionDecision Admission { get; set; }

        public string OfferNextTo { get; set; }

        public bool ExternalTransferSucceeds { get; set; } = true;

        public IvrStep StartStep { get; set; } = IvrStep.Done;

        public int Starts { get; private set; }

        public List<(string ActivityId, string QueueId, InteractionPriority? Priority)> Enqueued { get; } = [];

        public List<string> OfferedQueues { get; } = [];

        public List<(string ActivityId, string AgentId, int? RingTimeoutSeconds)> DirectOffers { get; } = [];

        public List<string> Treatments { get; } = [];

        public List<(string QueueId, string CallId)> WaitingAudio { get; } = [];

        public List<(string ActivityId, string Reason)> Voicemails { get; } = [];

        public List<string> ExternalTransfers { get; } = [];

        public List<(string EventType, CallLifecycleEventData Data)> Audit { get; } = [];

        public Task RouteAsync(IvrStep step)
            => Router.RouteAsync("interaction-1", EntryPoint, step, TestContext.Current.CancellationToken);
    }
}

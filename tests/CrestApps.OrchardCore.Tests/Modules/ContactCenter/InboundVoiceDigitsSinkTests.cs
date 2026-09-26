using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Tests.Modules.ContactCenter.Integration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// What happens when a caller presses a key on an entry-point menu. The flow decides what the key means; this is
/// the part that hands that decision to the router that actually puts the caller somewhere.
/// <para>
/// It used to act on a queue choice alone, by enqueueing without offering: an agent, voicemail or external choice
/// was saved, validated, and then did nothing, so the caller sat on the line.
/// </para>
/// </summary>
public sealed class InboundVoiceDigitsSinkTests
{
    [Theory]
    [InlineData(IvrStepKind.RouteToQueue, "queue-support")]
    [InlineData(IvrStepKind.RouteToAgent, "agent-7")]
    [InlineData(IvrStepKind.Voicemail, null)]
    [InlineData(IvrStepKind.ExternalTransfer, "dest-1")]
    [InlineData(IvrStepKind.Done, null)]
    public async Task EveryChoice_IsHandedToTheRouterWithTheEntryPoint(IvrStepKind kind, string targetId)
    {
        // Arrange
        var harness = new DigitsHarness();
        harness.WithMenuChoice(new IvrStep(kind, "root", null, null, targetId));

        // Act
        var handled = await harness.PressAsync("2");

        // Assert
        Assert.True(handled);
        var routed = harness.Routed.Single();
        Assert.Equal("interaction-1", routed.InteractionId);
        Assert.Equal("entry-1", routed.EntryPointId);
        Assert.Equal(kind, routed.Step.Kind);
        Assert.Equal(targetId, routed.Step.TargetId);
    }

    [Fact]
    public async Task AKeyPressOnACallNobodyKnows_IsNotOurs()
    {
        // Arrange
        // Another feature on the same tenant may be collecting digits for its own reasons. Reporting this as
        // handled would swallow their event.
        var harness = new DigitsHarness(interactionExists: false);

        // Act
        var handled = await harness.PressAsync("1");

        // Assert
        Assert.False(handled);
    }

    [Fact]
    public async Task AKeyPressOnACallWithNoMenu_IsNotOurs()
    {
        // Arrange
        var harness = new DigitsHarness();
        harness.EntryPointHasFlow = false;

        // Act
        var handled = await harness.PressAsync("1");

        // Assert
        Assert.False(handled);
        Assert.Empty(harness.Routed);
    }

    [Fact]
    public async Task AKeyPressAfterTheCallerLeftTheMenu_IsNotOurs()
    {
        // Arrange
        // A queue's callback offer collects a key on the same call as the menu did. Reading it as a menu choice
        // moved a caller already waiting for an agent.
        var harness = new DigitsHarness();
        harness.CompleteMenu();

        // Act
        var handled = await harness.PressAsync("1");

        // Assert
        Assert.False(handled);
        Assert.Empty(harness.Flow.Deliveries);
    }

    [Fact]
    public async Task PressingNothing_IsStillDeliveredToTheFlow()
    {
        // Arrange
        // A caller who says nothing has made a missed choice, and the flow is what decides whether that repeats
        // the menu or sends them to the fallback. Dropping the event leaves them in silence forever.
        var harness = new DigitsHarness();
        harness.WithMenuChoice(new IvrStep(IvrStepKind.Prompt, "root", "Press 1 for sales.", null, null));

        // Act
        await harness.PressAsync(digits: null, outcome: InboundVoiceDigitsOutcome.TimedOut);

        // Assert
        Assert.Null(harness.Flow.Deliveries.Single().Digits);
    }

    [Fact]
    public async Task AKeyTheMenuRefused_IsDeliveredAsAMissedChoice()
    {
        // Arrange
        var harness = new DigitsHarness();

        // Act
        await harness.PressAsync("7", outcome: InboundVoiceDigitsOutcome.Invalid);

        // Assert
        Assert.Equal("7", harness.Flow.Deliveries.Single().Digits);
    }

    [Fact]
    public async Task ACollectionThePlatformCancelled_DoesNotMoveTheCaller()
    {
        // Arrange
        // A newer command replaced the menu; reading its cancellation as a missed key counted a try against a caller
        // who did nothing.
        var harness = new DigitsHarness();

        // Act
        var handled = await harness.PressAsync(digits: null, outcome: InboundVoiceDigitsOutcome.Cancelled);

        // Assert
        Assert.True(handled);
        Assert.Empty(harness.Flow.Deliveries);
        Assert.Empty(harness.Routed);
    }

    [Fact]
    public async Task ACallerWhoHangsUpInTheMenu_IsRecordedAsAbandoned_AndNotRouted()
    {
        // Arrange
        // The platform answered the caller to play the menu. Without an abandon on record the reports counted a
        // caller who gave up in the menu as an answered call.
        var harness = new DigitsHarness();

        // Act
        var handled = await harness.PressAsync(digits: null, outcome: InboundVoiceDigitsOutcome.CallerHungUp);

        // Assert
        Assert.True(handled);
        Assert.Empty(harness.Routed);
        Assert.Equal(["CallerHungUp"], harness.Flow.Ended);
        Assert.Equal(ContactCenterConstants.Events.CallAbandoned, harness.Audit.Single().EventType);
        Assert.Equal($"call-abandoned:interaction-1", harness.Audit.Single().Key);
    }

    [Fact]
    public async Task TheDeliveryIdentity_IsPassedThroughSoARedeliveryIsRecognised()
    {
        // Arrange
        var harness = new DigitsHarness();
        harness.WithMenuChoice(new IvrStep(IvrStepKind.RouteToQueue, null, null, null, "queue-support"));

        // Act
        await harness.PressAsync("2", deliveryId: "delivery-7");

        // Assert
        Assert.Equal("delivery-7", harness.Flow.Deliveries.Single().DeliveryId);
    }

    private sealed class DigitsHarness
    {
        private readonly Interaction _interaction;

        public DigitsHarness(bool interactionExists = true)
        {
            _interaction = interactionExists
                ? new Interaction { ItemId = "interaction-1", ActivityItemId = "activity-1", ProviderInteractionId = "call-1" }
                : null;

            var interactionManager = new Mock<IInteractionManager>();
            interactionManager.Setup(x => x.FindByProviderInteractionIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(_interaction);

            var entryPointResolver = new Mock<IEntryPointFlowResolver>();
            entryPointResolver.Setup(x => x.FindEntryPointAsync(It.IsAny<Interaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new ContactCenterEntryPoint
                {
                    ItemId = "entry-1",
                    IvrFlow = EntryPointHasFlow ? new IvrFlow { RootNodeId = "root" } : null,
                });

            var router = new Mock<IIvrCallRouter>();
            router.Setup(x => x.RouteAsync(It.IsAny<string>(), It.IsAny<ContactCenterEntryPoint>(), It.IsAny<IvrStep>(), It.IsAny<CancellationToken>()))
                .Callback<string, ContactCenterEntryPoint, IvrStep, CancellationToken>((id, entryPoint, step, _) => Routed.Add((id, entryPoint.ItemId, step)))
                .Returns(Task.CompletedTask);

            var audit = new Mock<IContactCenterAuditRecorder>();
            audit.Setup(x => x.RecordCallAsync(
                    It.IsAny<string>(),
                    It.IsAny<CallLifecycleEventData>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<ContactCenterActor>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, CallLifecycleEventData, DateTime, ContactCenterActor, string, CancellationToken>((eventType, _, _, _, key, _) => Audit.Add((eventType, key)))
                .Returns(Task.CompletedTask);

            Sink = new InboundVoiceDigitsSink(
                interactionManager.Object,
                entryPointResolver.Object,
                Flow,
                router.Object,
                audit.Object,
                new TestClock(),
                NullLogger<InboundVoiceDigitsSink>.Instance);
        }

        public RecordingIvrExecution Flow { get; } = new();

        public InboundVoiceDigitsSink Sink { get; }

        public bool EntryPointHasFlow { get; set; } = true;

        public List<(string InteractionId, string EntryPointId, IvrStep Step)> Routed { get; } = [];

        public List<(string EventType, string Key)> Audit { get; } = [];

        public void WithMenuChoice(IvrStep step)
            => Flow.NextStep = step;

        public void CompleteMenu()
            => _interaction.TechnicalMetadata[IvrExecutionService.StateMetadataKey] = new IvrFlowState { CurrentNodeId = "root", Completed = true };

        public Task<bool> PressAsync(string digits, string deliveryId = "delivery-1", InboundVoiceDigitsOutcome outcome = InboundVoiceDigitsOutcome.Collected)
            => Sink.HandleDigitsAsync(new InboundVoiceDigitsEvent
            {
                ProviderName = "Telnyx",
                ProviderCallId = "call-1",
                Digits = digits,
                DeliveryId = deliveryId,
                Outcome = outcome,
            }, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// An IVR execution that records what it was asked and answers with a scripted step.
    /// </summary>
    private sealed class RecordingIvrExecution : IIvrExecutionService
    {
        public List<(string Digits, string DeliveryId)> Deliveries { get; } = [];

        public List<string> Ended { get; } = [];

        public IvrStep NextStep { get; set; } = IvrStep.Done;

        public Task<IvrStep> StartAsync(Interaction interaction, IvrFlow flow, CancellationToken cancellationToken = default)
            => Task.FromResult(NextStep);

        public Task<IvrStep> HandleDigitsAsync(Interaction interaction, IvrFlow flow, string digits, string deliveryId, CancellationToken cancellationToken = default)
        {
            Deliveries.Add((digits, deliveryId));

            return Task.FromResult(NextStep);
        }

        public Task EndAsync(Interaction interaction, string reason, CancellationToken cancellationToken = default)
        {
            Ended.Add(reason);

            return Task.CompletedTask;
        }
    }
}

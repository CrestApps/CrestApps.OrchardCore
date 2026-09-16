using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// What happens when a caller presses a key on an entry-point menu. The flow decides what the key means; this is
/// the part that turns that decision into the caller actually being put somewhere.
/// </summary>
public sealed class InboundVoiceDigitsSinkTests
{
    [Fact]
    public async Task ChoosingAnOption_PutsTheCallerInThatQueue()
    {
        // Arrange
        var harness = new DigitsHarness();
        harness.WithMenuChoice(new IvrStep(IvrStepKind.RouteToQueue, null, null, null, "queue-support"));

        // Act
        var handled = await harness.PressAsync("2");

        // Assert
        Assert.True(handled);
        Assert.Equal(("activity-1", "queue-support"), harness.Enqueued.Single());
    }

    [Fact]
    public async Task StillInTheMenu_NobodyIsQueuedYet()
    {
        // Arrange
        // A sub-menu is not a destination. Queueing the caller here would put them in line while they are still
        // being asked where they want to go.
        var harness = new DigitsHarness();
        harness.WithMenuChoice(new IvrStep(IvrStepKind.Prompt, "products", "Press 1 for new.", null, null));

        // Act
        var handled = await harness.PressAsync("1");

        // Assert
        Assert.True(handled);
        Assert.Empty(harness.Enqueued);
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
        Assert.Empty(harness.Enqueued);
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
        await harness.PressAsync(digits: null);

        // Assert
        Assert.Single(harness.Flow.Deliveries);
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
        private readonly List<(string ActivityId, string QueueId)> _enqueued = [];

        public DigitsHarness(bool interactionExists = true)
        {
            var interaction = interactionExists
                ? new Interaction { ItemId = "interaction-1", ActivityItemId = "activity-1", ProviderInteractionId = "call-1" }
                : null;

            var interactionManager = new Mock<IInteractionManager>();
            interactionManager.Setup(x => x.FindByProviderInteractionIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(interaction);

            var entryPointResolver = new Mock<IEntryPointFlowResolver>();
            entryPointResolver.Setup(x => x.FindFlowAsync(It.IsAny<Interaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => EntryPointHasFlow ? new IvrFlow { RootNodeId = "root" } : null);

            var queueService = new Mock<IActivityQueueService>();
            queueService.Setup(x => x.EnqueueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<InteractionPriority?>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, InteractionPriority?, CancellationToken>((activityId, queueId, _, _) => _enqueued.Add((activityId, queueId)))
                .ReturnsAsync(new QueueItem());

            Sink = new InboundVoiceDigitsSink(
                interactionManager.Object,
                entryPointResolver.Object,
                Flow,
                queueService.Object,
                NullLogger<InboundVoiceDigitsSink>.Instance);
        }

        public RecordingIvrExecution Flow { get; } = new();

        public InboundVoiceDigitsSink Sink { get; }

        public bool EntryPointHasFlow { get; set; } = true;

        public IReadOnlyList<(string ActivityId, string QueueId)> Enqueued => _enqueued;

        public void WithMenuChoice(IvrStep step)
            => Flow.NextStep = step;

        public Task<bool> PressAsync(string digits, string deliveryId = "delivery-1")
            => Sink.HandleDigitsAsync(new InboundVoiceDigitsEvent
            {
                ProviderName = "Telnyx",
                ProviderCallId = "call-1",
                Digits = digits,
                DeliveryId = deliveryId,
            }, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// An IVR execution that records what it was asked and answers with a scripted step.
    /// </summary>
    private sealed class RecordingIvrExecution : IIvrExecutionService
    {
        public List<(string Digits, string DeliveryId)> Deliveries { get; } = [];

        public IvrStep NextStep { get; set; } = IvrStep.Done;

        public Task<IvrStep> StartAsync(Interaction interaction, IvrFlow flow, CancellationToken cancellationToken = default)
            => Task.FromResult(NextStep);

        public Task<IvrStep> HandleDigitsAsync(Interaction interaction, IvrFlow flow, string digits, string deliveryId, CancellationToken cancellationToken = default)
        {
            Deliveries.Add((digits, deliveryId));

            return Task.FromResult(NextStep);
        }
    }
}

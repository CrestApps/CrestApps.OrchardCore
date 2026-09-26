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
/// Running a caller through a phone menu. The state machine decides what comes next and is already covered on its
/// own; this is the part that plays the prompt, remembers where the caller is across webhooks and restarts, and
/// hands the routing decision back when they have chosen.
/// <para>
/// The persistence is the point: provider gather events are at-least-once, so acting twice on one key press would
/// take a caller two levels into a menu they navigated once.
/// </para>
/// </summary>
public sealed class IvrExecutionServiceTests
{
    [Fact]
    public async Task AnEntryPointWithNoMenu_RoutesTheWayItAlreadyDid()
    {
        // Arrange
        // Every tenant that exists today has no menu. Starting a flow for them must change nothing at all.
        var harness = new IvrHarness();

        // Act
        var outcome = await harness.StartAsync(new ContactCenterEntryPoint { ItemId = "entry-1" });

        // Assert
        Assert.Equal(IvrStepKind.Done, outcome.Kind);
        Assert.Empty(harness.Provider.Prompts);
    }

    [Fact]
    public async Task AMenu_IsPlayedToTheCallerWithItsKeys()
    {
        // Arrange
        var harness = new IvrHarness();

        // Act
        var outcome = await harness.StartAsync(SalesOrSupport());

        // Assert
        Assert.Equal(IvrStepKind.Prompt, outcome.Kind);
        Assert.Equal("Press 1 for sales, 2 for support.", harness.Provider.Prompts.Single().Text);
        Assert.Equal("12", harness.Provider.Prompts.Single().ValidDigits);
    }

    [Fact]
    public async Task WhereTheCallerIs_IsRememberedOnTheInteraction()
    {
        // Arrange
        // A menu that forgets where the caller is starts them at the top again on the next event, which after a
        // restart means a caller who chose "support" hears the main menu instead.
        var harness = new IvrHarness();

        // Act
        await harness.StartAsync(SalesOrSupport());

        // Assert
        Assert.Equal("root", harness.ReadState().CurrentNodeId);
    }

    [Fact]
    public async Task AKeyPress_RoutesTheCallerToWhatTheyChose()
    {
        // Arrange
        var harness = new IvrHarness();
        await harness.StartAsync(SalesOrSupport());

        // Act
        var outcome = await harness.HandleDigitsAsync(SalesOrSupport(), "2", "delivery-1");

        // Assert
        Assert.Equal(IvrStepKind.RouteToQueue, outcome.Kind);
        Assert.Equal("queue-support", outcome.TargetId);
    }

    [Fact]
    public async Task TheSameKeyPressDeliveredTwice_MovesTheCallerOnce()
    {
        // Arrange
        // Provider webhooks are at-least-once. Acting twice takes the caller two levels into a menu they
        // navigated once.
        var harness = new IvrHarness();
        await harness.StartAsync(MenuWithSubMenu());

        // Act
        var first = await harness.HandleDigitsAsync(MenuWithSubMenu(), "1", "delivery-1");
        var second = await harness.HandleDigitsAsync(MenuWithSubMenu(), "1", "delivery-1");

        // Assert
        // The redelivery re-plays the menu the caller is on rather than reporting nothing, so a duplicate webhook
        // leaves them hearing their options instead of silence - but it does not take them a level deeper.
        Assert.Equal(IvrStepKind.Prompt, first.Kind);
        Assert.Equal(IvrStepKind.Prompt, second.Kind);
        Assert.Equal("products", first.NodeId);
        Assert.Equal("products", second.NodeId);
        Assert.Equal("products", harness.ReadState().CurrentNodeId);
    }

    [Fact]
    public async Task AWrongKey_RepeatsTheMenuRatherThanDroppingTheCaller()
    {
        // Arrange
        var harness = new IvrHarness();
        await harness.StartAsync(SalesOrSupport());

        // Act
        var outcome = await harness.HandleDigitsAsync(SalesOrSupport(), "9", "delivery-1");

        // Assert
        Assert.Equal(IvrStepKind.Prompt, outcome.Kind);
        Assert.Equal(2, harness.Provider.Prompts.Count);
    }

    [Fact]
    public async Task ACallerWhoKeepsMissing_IsSentSomewhereRatherThanLoopingForever()
    {
        // Arrange
        // A menu that repeats indefinitely is a caller who never reaches a person.
        var harness = new IvrHarness();
        var flow = SalesOrSupport();
        flow.MaxRetries = 2;
        await harness.StartAsync(flow);

        // Act
        await harness.HandleDigitsAsync(flow, "9", "delivery-1");
        var outcome = await harness.HandleDigitsAsync(flow, "9", "delivery-2");

        // Assert
        Assert.Equal(IvrStepKind.RouteToQueue, outcome.Kind);
        Assert.Equal("queue-fallback", outcome.TargetId);
    }

    [Fact]
    public async Task ARoutingChoice_StopsPromptingTheCaller()
    {
        // Arrange
        // Speaking another menu at somebody who has already been sent to a queue talks over their hold music.
        var harness = new IvrHarness();
        await harness.StartAsync(SalesOrSupport());
        harness.Provider.Prompts.Clear();

        // Act
        await harness.HandleDigitsAsync(SalesOrSupport(), "1", "delivery-1");

        // Assert
        Assert.Empty(harness.Provider.Prompts);
    }

    [Fact]
    public async Task WithNoLiveLeg_NothingIsPlayed()
    {
        // Arrange
        // An interaction with no provider call has nothing to prompt on; the caller is routed the way the entry
        // point already said to rather than being held in a menu nobody can hear.
        var harness = new IvrHarness(providerCallId: null);

        // Act
        var outcome = await harness.StartAsync(SalesOrSupport());

        // Assert
        Assert.Equal(IvrStepKind.Done, outcome.Kind);
        Assert.Empty(harness.Provider.Prompts);
    }

    private static IvrFlow SalesOrSupport()
        => new()
        {
            RootNodeId = "root",
            MaxRetries = 3,
            FallbackAction = new IvrAction { Kind = IvrActionKind.RouteToQueue, TargetId = "queue-fallback" },
            Nodes =
            [
                new IvrNode
                {
                    NodeId = "root",
                    Prompt = "Press 1 for sales, 2 for support.",
                    Options =
                    [
                        new IvrOption { Digit = "1", Action = new IvrAction { Kind = IvrActionKind.RouteToQueue, TargetId = "queue-sales" } },
                        new IvrOption { Digit = "2", Action = new IvrAction { Kind = IvrActionKind.RouteToQueue, TargetId = "queue-support" } },
                    ],
                },
            ],
        };

    private static IvrFlow MenuWithSubMenu()
        => new()
        {
            RootNodeId = "root",
            MaxRetries = 3,
            FallbackAction = new IvrAction { Kind = IvrActionKind.RouteToQueue, TargetId = "queue-fallback" },
            Nodes =
            [
                new IvrNode
                {
                    NodeId = "root",
                    Prompt = "Press 1 for products.",
                    Options = [new IvrOption { Digit = "1", Action = new IvrAction { Kind = IvrActionKind.SubMenu, TargetId = "products" } }],
                },
                new IvrNode
                {
                    NodeId = "products",
                    Prompt = "Press 1 for new, 2 for used.",
                    Options = [new IvrOption { Digit = "1", Action = new IvrAction { Kind = IvrActionKind.RouteToQueue, TargetId = "queue-new" } }],
                },
            ],
        };

    [Fact]
    public async Task StartingAMenu_AnswersTheCallerBeforePlayingIt()
    {
        // Arrange
        // A ringing call cannot hear anything, and Telnyx refuses to collect digits on one: the menu was never
        // audible because nothing answered the caller first.
        var harness = new IvrHarness();

        // Act
        await harness.StartAsync(SalesOrSupport());

        // Assert
        Assert.Equal(["commit", "answer:call-1", "prompt:root"], harness.Sequence);
    }

    [Fact]
    public async Task AKeyPress_CommitsTheCallersPositionBeforeTheNextMenuIsPlayed()
    {
        // Arrange
        // The provider's events for the call write the same interaction. A position still uncommitted when the next
        // menu is already playing is lost to that conflict, and the caller's next key is read against the wrong menu.
        var harness = new IvrHarness();
        await harness.StartAsync(MenuWithSubMenu());
        harness.Sequence.Clear();

        // Act
        await harness.HandleDigitsAsync(MenuWithSubMenu(), "1", "delivery-1");

        // Assert
        Assert.Equal(["commit", "prompt:products"], harness.Sequence);
    }

    [Fact]
    public async Task ARoutingChoice_CommitsWithTheRoutingRatherThanAheadOfIt()
    {
        // Arrange
        // Committed on its own, a choice whose routing then lost a concurrency conflict was retried against a caller
        // already marked as out of the menu, and the retry put them nowhere.
        var harness = new IvrHarness();
        await harness.StartAsync(SalesOrSupport());
        harness.Sequence.Clear();

        // Act
        await harness.HandleDigitsAsync(SalesOrSupport(), "2", "delivery-1");

        // Assert
        Assert.DoesNotContain("commit", harness.Sequence);
        Assert.True(harness.ReadState().Completed);
    }

    [Fact]
    public async Task AMenuThatCannotBePlayed_SendsTheCallerToTheEntryPointsTarget()
    {
        // Arrange
        // A refused prompt is a caller on a silent line waiting for a key press that cannot come.
        var harness = new IvrHarness();
        harness.Provider.PromptSucceeds = false;

        // Act
        var outcome = await harness.StartAsync(SalesOrSupport());

        // Assert
        Assert.Equal(IvrStepKind.Done, outcome.Kind);
        Assert.True(outcome.IsFallback);
        Assert.True(harness.ReadState().Completed);
    }

    [Fact]
    public async Task OnceTheCallerHasBeenRouted_LaterKeyPressesDoNothing()
    {
        // Arrange
        // A queue's callback offer collects a key on the same call. Read as a menu choice, it moved a caller who was
        // already waiting for an agent.
        var harness = new IvrHarness();
        await harness.StartAsync(SalesOrSupport());
        await harness.HandleDigitsAsync(SalesOrSupport(), "1", "delivery-1");
        harness.Provider.Prompts.Clear();

        // Act
        var outcome = await harness.HandleDigitsAsync(SalesOrSupport(), "2", "delivery-2");

        // Assert
        Assert.Equal(IvrStepKind.Ignored, outcome.Kind);
        Assert.Empty(harness.Provider.Prompts);
    }

    [Fact]
    public async Task TheCallersRoute_IsRecordedAsTheyGo()
    {
        // Arrange
        var harness = new IvrHarness();
        await harness.StartAsync(MenuWithSubMenu());

        // Act
        await harness.HandleDigitsAsync(MenuWithSubMenu(), "1", "delivery-1");
        await harness.HandleDigitsAsync(MenuWithSubMenu(), "1", "delivery-2");

        // Assert
        Assert.Equal(
            [
                ContactCenterConstants.Events.IvrMenuEntered,
                ContactCenterConstants.Events.IvrDigitsReceived,
                ContactCenterConstants.Events.IvrMenuEntered,
                ContactCenterConstants.Events.IvrDigitsReceived,
                ContactCenterConstants.Events.IvrActionTaken,
            ],
            harness.Audit.Select(entry => entry.EventType));
        Assert.Equal("queue-new", harness.Audit[^1].Data.Target);
        Assert.Equal(["Menu", "Menu:products", "RouteToQueue:queue-new"], harness.ReadState().Path.Select(entry => entry.Result));
    }

    [Fact]
    public async Task RunningOutOfTries_IsRecordedAsTheFallback()
    {
        // Arrange
        var harness = new IvrHarness();
        var flow = SalesOrSupport();
        flow.MaxRetries = 1;
        await harness.StartAsync(flow);

        // Act
        await harness.HandleDigitsAsync(flow, digits: null, "delivery-1");

        // Assert
        Assert.Equal(ContactCenterConstants.Events.IvrFallbackTaken, harness.Audit[^1].EventType);
        Assert.Equal("RetriesExhausted", harness.Audit[^1].Data.Reason);
    }

    [Fact]
    public async Task ARedeliveredKeyPress_IsNotRecordedTwice()
    {
        // Arrange
        var harness = new IvrHarness();
        await harness.StartAsync(MenuWithSubMenu());
        await harness.HandleDigitsAsync(MenuWithSubMenu(), "1", "delivery-1");

        // Act
        await harness.HandleDigitsAsync(MenuWithSubMenu(), "1", "delivery-1");

        // Assert
        Assert.Single(harness.Audit, entry => entry.EventType == ContactCenterConstants.Events.IvrDigitsReceived);
        Assert.Equal(2, harness.ReadState().Path.Count);
    }

    [Fact]
    public void TheCallersPosition_SurvivesTheInteractionBeingReloaded()
    {
        // Arrange
        // YesSql hands an untyped metadata value back as an ExpandoObject. Reading it through ToString() threw, so a
        // reloaded caller had no position and their next key press sent them to the entry point's target.
        var interaction = new Interaction { ItemId = "interaction-1" };
        dynamic stored = new System.Dynamic.ExpandoObject();
        stored.CurrentNodeId = "support";
        stored.Attempts = 2L;
        stored.AppliedDeliveryIds = new List<object> { "delivery-1" };
        stored.Completed = false;
        interaction.TechnicalMetadata[IvrExecutionService.StateMetadataKey] = stored;

        // Act
        var state = IvrExecutionService.ReadState(interaction);

        // Assert
        Assert.Equal("support", state.CurrentNodeId);
        Assert.Equal(2, state.Attempts);
        Assert.Equal(["delivery-1"], state.AppliedDeliveryIds);
    }

    private sealed class IvrHarness
    {
        private readonly Interaction _interaction;
        private readonly Mock<IInteractionManager> _interactionManager = new();

        public IvrHarness(string providerCallId = "call-1")
        {
            _interaction = new Interaction
            {
                ItemId = "interaction-1",
                ProviderInteractionId = providerCallId,
            };

            _interactionManager.Setup(x => x.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(_interaction);

            Provider = new RecordingIvrProvider(Sequence);

            var session = new Mock<global::YesSql.ISession>();
            session.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Callback(() => Sequence.Add("commit"))
                .Returns(Task.CompletedTask);

            var audit = new Mock<IContactCenterAuditRecorder>();
            audit.Setup(x => x.RecordCallAsync(
                    It.IsAny<string>(),
                    It.IsAny<CallLifecycleEventData>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<ContactCenterActor>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, CallLifecycleEventData, DateTime, ContactCenterActor, string, CancellationToken>((eventType, data, _, _, key, _) =>
                {
                    // The real recorder is idempotent on the key; so is this one.
                    if (!Audit.Any(entry => entry.Key == key))
                    {
                        Audit.Add((eventType, data, key));
                    }
                })
                .Returns(Task.CompletedTask);

            Service = new IvrExecutionService(
                _interactionManager.Object,
                Provider,
                audit.Object,
                session.Object,
                new TestClock(),
                NullLogger<IvrExecutionService>.Instance);
        }

        public List<string> Sequence { get; } = [];

        public List<(string EventType, CallLifecycleEventData Data, string Key)> Audit { get; } = [];

        public RecordingIvrProvider Provider { get; }

        public IvrExecutionService Service { get; }

        public Task<IvrStep> StartAsync(ContactCenterEntryPoint entryPoint)
            => Service.StartAsync(_interaction, entryPoint.IvrFlow, TestContext.Current.CancellationToken);

        public Task<IvrStep> StartAsync(IvrFlow flow)
            => Service.StartAsync(_interaction, flow, TestContext.Current.CancellationToken);

        public Task<IvrStep> HandleDigitsAsync(IvrFlow flow, string digits, string deliveryId)
            => Service.HandleDigitsAsync(_interaction, flow, digits, deliveryId, TestContext.Current.CancellationToken);

        public IvrFlowState ReadState()
            => IvrExecutionService.ReadState(_interaction);
    }

    /// <summary>
    /// An IVR provider that records what the caller would have heard.
    /// </summary>
    private sealed class RecordingIvrProvider : IIvrProvider
    {
        private readonly List<string> _sequence;

        public RecordingIvrProvider(List<string> sequence)
        {
            _sequence = sequence;
        }

        public List<(string Text, string ValidDigits)> Prompts { get; } = [];

        public bool PromptSucceeds { get; set; } = true;

        public Task<bool> AnswerAsync(string providerCallId, CancellationToken cancellationToken = default)
        {
            _sequence.Add($"answer:{providerCallId}");

            return Task.FromResult(true);
        }

        public Task<bool> PromptAsync(string providerCallId, string text, string mediaId, string validDigits, CancellationToken cancellationToken = default)
        {
            Prompts.Add((text, validDigits));
            _sequence.Add($"prompt:{(text.StartsWith("Press 1 for new", StringComparison.Ordinal) ? "products" : "root")}");

            return Task.FromResult(PromptSucceeds);
        }
    }
}

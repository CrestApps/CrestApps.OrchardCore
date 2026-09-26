using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;
using YesSql;
using ProviderVoiceEvent = CrestApps.OrchardCore.Telephony.Models.ProviderVoiceEvent;
using VoiceCallState = CrestApps.OrchardCore.Telephony.Models.VoiceCallState;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterTransferServiceTests
{
    private static readonly DateTime _now = new(2026, 1, 5, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task TransferAsync_ToAQueue_IsRoutedByTheContactCenter_NotHandedToTheProvider()
    {
        var harness = new Harness();
        harness.Router
            .Setup(router => router.RouteToQueueAsync(It.IsAny<TransferRoutingContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TransferResult.Success("The call is waiting in Sales."));

        var result = await harness.Service.TransferAsync(Request(InteractionTransferTargetType.Queue, "q2"), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        harness.Router.Verify(router => router.RouteToQueueAsync(
            It.Is<TransferRoutingContext>(context =>
                context.TargetId == "q2" &&
                context.TransferringAgentId == "a1" &&
                context.TransferringUserId == "user-1" &&
                context.Interaction.ItemId == "int-1"),
            It.IsAny<CancellationToken>()), Times.Once);
        harness.TransferProvider.Verify(
            provider => provider.TransferAsync(It.IsAny<ContactCenterVoiceTransferRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TransferAsync_ToAnAgent_IsRoutedByTheContactCenter_NotHandedToTheProvider()
    {
        var harness = new Harness();
        harness.Router
            .Setup(router => router.RouteToAgentAsync(It.IsAny<TransferRoutingContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TransferResult.Success());

        var result = await harness.Service.TransferAsync(Request(InteractionTransferTargetType.Agent, "a2"), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        harness.Router.Verify(router => router.RouteToAgentAsync(It.Is<TransferRoutingContext>(context => context.TargetId == "a2"), It.IsAny<CancellationToken>()), Times.Once);
        harness.TransferProvider.Verify(
            provider => provider.TransferAsync(It.IsAny<ContactCenterVoiceTransferRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TransferAsync_ToAnExternalNumber_MovesTheCallThroughTheProvider_AndSettlesItAsTransferred()
    {
        var harness = new Harness();
        harness.TransferProvider
            .Setup(provider => provider.TransferAsync(It.IsAny<ContactCenterVoiceTransferRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactCenterVoiceProviderResult { Succeeded = true });

        var result = await harness.Service.TransferAsync(Request(InteractionTransferTargetType.External, "+15557654321"), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        harness.TransferProvider.Verify(provider => provider.TransferAsync(
            It.Is<ContactCenterVoiceTransferRequest>(request =>
                request.InteractionId == "int-1" &&
                request.ProviderCallId == "call-1" &&
                request.TransferType == InteractionTransferType.Blind &&
                request.TargetType == InteractionTransferTargetType.External &&
                request.Target == "+15557654321"),
            It.IsAny<CancellationToken>()), Times.Once);

        // The call has left the contact center, and it leaves through provider truth like any other ending.
        harness.VoiceEvents.Verify(service => service.IngestAsync(
            It.Is<ProviderVoiceEvent>(voiceEvent => voiceEvent.State == VoiceCallState.Transferred && voiceEvent.ProviderCallId == "call-1"),
            It.IsAny<CancellationToken>()), Times.Once);

        var entry = Assert.Single(harness.Interaction.TransferHistory);
        Assert.Equal("a1", entry.FromParticipantId);
        Assert.Equal("+15557654321", entry.ToParticipantId);
        Assert.Equal(_now, entry.CompletedUtc);
        Assert.Equal(InteractionTransferHistory.SentToExternalNumber, entry.Result);

        var transferred = harness.Published.Single(e => e.EventType == ContactCenterConstants.Events.InteractionTransferred);
        var data = transferred.GetData<CallLifecycleEventData>();
        Assert.Equal("+15557654321", data.Target);
        Assert.Equal("a1", data.AgentId);
        Assert.Equal(nameof(InteractionTransferTargetType.External), data.Details["targetType"]);
        Assert.Equal("user-1", transferred.ActorId);
        Assert.Equal(ContactCenterActorType.Agent, transferred.ActorType);

        // The agent's own leg is hung up only once the call is settled, so its hangup cannot end the call.
        harness.AgentRelease.Verify(release => release.HangUpAsync("provider", It.Is<IEnumerable<string>>(legs => legs.Single() == "agent-leg-1"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TransferAsync_WhenTheDestinationIsRefused_RecordsTheDenialAndMovesNothing()
    {
        var harness = new Harness(new FakeTransferDestinationResolver(_ => TransferDestinationResolutionResult.Denied("Not on the approved list.")));

        var result = await harness.Service.TransferAsync(Request(InteractionTransferTargetType.External, "+15557654321"), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("Not on the approved list.", result.Reason);
        Assert.Single(harness.Published, e => e.EventType == ContactCenterConstants.Events.InteractionTransferDenied);
        harness.TransferProvider.VerifyNoOtherCalls();
        harness.Router.VerifyNoOtherCalls();
        Assert.Empty(harness.Interaction.TransferHistory);
    }

    [Fact]
    public async Task TransferAsync_AWarmTransfer_IsNotBlindTransferredInstead()
    {
        var harness = new Harness();
        var request = Request(InteractionTransferTargetType.Agent, "a2");
        request.Type = InteractionTransferType.Consultative;

        var result = await harness.Service.TransferAsync(request, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        harness.Router.VerifyNoOtherCalls();
        harness.TransferProvider.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TransferAsync_WhenTheProviderRefusesAnExternalTransfer_RecordsNothing()
    {
        var harness = new Harness();
        harness.TransferProvider
            .Setup(provider => provider.TransferAsync(It.IsAny<ContactCenterVoiceTransferRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactCenterVoiceProviderResult { Succeeded = false, ErrorMessage = "Transfer rejected." });

        var result = await harness.Service.TransferAsync(Request(InteractionTransferTargetType.External, "+15557654321"), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("Transfer rejected.", result.Reason);
        Assert.Empty(harness.Interaction.TransferHistory);
        Assert.Empty(harness.Published);
        harness.VoiceEvents.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TransferAsync_WhenTheProviderOutcomeIsUnknown_RecordsNothing()
    {
        var harness = new Harness();
        harness.TransferProvider
            .Setup(provider => provider.TransferAsync(It.IsAny<ContactCenterVoiceTransferRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactCenterVoiceProviderResult { Succeeded = true, OutcomeUnknown = true, ErrorMessage = "The provider outcome is unknown." });

        var result = await harness.Service.TransferAsync(Request(InteractionTransferTargetType.External, "+15557654321"), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("The provider outcome is unknown.", result.Reason);
        Assert.Empty(harness.Interaction.TransferHistory);
        harness.VoiceEvents.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TransferAsync_WhenTheProviderDeadlineExpires_ReturnsUnknownWithoutRecording()
    {
        var harness = new Harness(commandExecutor: new TimeoutTelephonyCommandExecutor());

        var result = await harness.Service.TransferAsync(Request(InteractionTransferTargetType.External, "+15557654321"), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.True(result.OutcomeUnknown);
        Assert.Empty(harness.Interaction.TransferHistory);
        Assert.Empty(harness.Published);
    }

    [Fact]
    public async Task TransferAsync_WhenTheCallerDisconnects_TheProviderStillGetsAServerOwnedToken()
    {
        using var callerCancellation = new CancellationTokenSource();
        var harness = new Harness();
        harness.TransferProvider
            .Setup(provider => provider.TransferAsync(It.IsAny<ContactCenterVoiceTransferRequest>(), It.IsAny<CancellationToken>()))
            .Returns<ContactCenterVoiceTransferRequest, CancellationToken>((_, cancellationToken) =>
            {
                Assert.NotEqual(callerCancellation.Token, cancellationToken);
                callerCancellation.Cancel();

                return Task.FromResult(new ContactCenterVoiceProviderResult { Succeeded = true });
            });

        var result = await harness.Service.TransferAsync(Request(InteractionTransferTargetType.External, "+15557654321"), callerCancellation.Token);

        Assert.True(result.Succeeded);
        Assert.True(callerCancellation.IsCancellationRequested);
    }

    [Fact]
    public async Task TransferAsync_WhenTheProviderCannotTransfer_FailsClosed()
    {
        var harness = new Harness(providerTransfers: false);

        var result = await harness.Service.TransferAsync(Request(InteractionTransferTargetType.External, "+15557654321"), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Empty(harness.Interaction.TransferHistory);
        Assert.Empty(harness.Published);
    }

    [Fact]
    public async Task TransferAsync_WhenTheInteractionIsMissing_Fails()
    {
        var harness = new Harness();

        var request = Request(InteractionTransferTargetType.Queue, "q2");
        request.InteractionId = "missing";

        var result = await harness.Service.TransferAsync(request, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        harness.Router.VerifyNoOtherCalls();
    }

    private static TransferRequest Request(InteractionTransferTargetType targetType, string targetId)
        => new()
        {
            InteractionId = "int-1",
            InitiatedByUserId = "user-1",
            Type = InteractionTransferType.Blind,
            TargetType = targetType,
            TargetId = targetId,
        };

    private sealed class Harness
    {
        public Interaction Interaction { get; } = new()
        {
            ItemId = "int-1",
            ActivityItemId = "act-1",
            AgentId = "a1",
            ProviderName = "provider",
            ProviderInteractionId = "call-1",
        };

        public CallSession Session { get; } = new()
        {
            ItemId = "session-1",
            InteractionId = "int-1",
            ProviderName = "provider",
            ProviderCallId = "call-1",
            AgentId = "a1",
            Legs =
            [
                new CallLeg { ProviderLegId = "call-1", Role = CallPartyRole.Customer },
                new CallLeg { ProviderLegId = "agent-leg-1", Role = CallPartyRole.Agent, AgentId = "a1", AnsweredUtc = _now },
            ],
        };

        public Mock<ITransferredCallRouter> Router { get; } = new();

        public Mock<ITransferAgentReleaseService> AgentRelease { get; } = new();

        public Mock<IProviderVoiceEventService> VoiceEvents { get; } = new();

        public Mock<IContactCenterVoiceTransferProvider> TransferProvider { get; }

        public List<InteractionEvent> Published { get; } = [];

        public ContactCenterTransferService Service { get; }

        public Harness(
            ITransferDestinationResolver destinationResolver = null,
            ITelephonyCommandExecutor commandExecutor = null,
            bool providerTransfers = true)
        {
            var interactionManager = new Mock<IInteractionManager>();
            interactionManager.Setup(manager => manager.FindByIdAsync("int-1", It.IsAny<CancellationToken>())).ReturnsAsync(Interaction);

            var provider = new Mock<IContactCenterVoiceProvider>();
            provider.SetupGet(value => value.Capabilities).Returns(ContactCenterVoiceProviderCapabilities.CallTransfer);
            TransferProvider = providerTransfers ? provider.As<IContactCenterVoiceTransferProvider>() : new Mock<IContactCenterVoiceTransferProvider>();

            var resolver = new Mock<IContactCenterVoiceProviderResolver>();
            resolver.Setup(value => value.Get("provider")).Returns(provider.Object);

            var publisher = new Mock<IContactCenterEventPublisher>();
            publisher
                .Setup(value => value.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
                .Callback<InteractionEvent, CancellationToken>((interactionEvent, _) => Published.Add(interactionEvent))
                .Returns(Task.CompletedTask);

            var clock = new Mock<IClock>();
            clock.SetupGet(value => value.UtcNow).Returns(_now);

            var authorization = new FakeCallControlAuthorizationService(context => new CallControlAuthorizationResult
            {
                Succeeded = true,
                AgentId = "a1",
                ProviderCallId = "call-1",
                CallSession = Session,
            });

            Service = new ContactCenterTransferService(
                interactionManager.Object,
                resolver.Object,
                authorization,
                destinationResolver ?? new FakeTransferDestinationResolver(),
                Router.Object,
                AgentRelease.Object,
                VoiceEvents.Object,
                publisher.Object,
                commandExecutor ?? new DefaultTelephonyCommandExecutor(Options.Create(new TelephonyCommandOptions()), Mock.Of<IHostApplicationLifetime>()),
                Mock.Of<ISession>(),
                clock.Object,
                Mock.Of<IContactCenterMonitoringService>());
        }
    }

    private sealed class TimeoutTelephonyCommandExecutor : ITelephonyCommandExecutor
    {
        public Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> operation)
            => Task.FromException<TResult>(new TimeoutException());
    }
}

#nullable enable annotations

using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class AgentPreDialCoordinatorTests
{
    private static readonly DateTime _now = new(2026, 9, 23, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task PreDialAsync_WhenAPendingVoiceOfferIsPresented_RingsTheAgentForTheOffersRemainingLifetime()
    {
        // Arrange
        var harness = new Harness();
        ContactCenterAgentPreDialRequest? request = null;
        harness.PreDialProvider
            .Setup(provider => provider.PreDialAgentAsync(It.IsAny<ContactCenterAgentPreDialRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ContactCenterAgentPreDialRequest, CancellationToken>((value, _) => request = value)
            .ReturnsAsync(Dialed("leg-1"));

        // Act
        var dialed = await harness.Coordinator.PreDialAsync("r1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(dialed);
        Assert.NotNull(request);
        Assert.Equal("r1", request!.ReservationId);
        Assert.Equal("call-1", request.ProviderCallId);
        Assert.Equal("u1", request.AgentUserId);

        // The offer has 20 seconds left; the leg rings that long plus the grace for a last-moment accept.
        Assert.Equal(25, request.TimeoutSeconds);

        var leg = await harness.Store.FindAsync("r1", TestContext.Current.CancellationToken);
        Assert.NotNull(leg);
        Assert.Equal("leg-1", leg!.AgentLegId);
        Assert.Equal("call-1", leg.ProviderCallId);
    }

    [Fact]
    public async Task PreDialAsync_WhenTheProviderCannotPreDial_LeavesTheAcceptToConnectTheAgent()
    {
        // Arrange
        var harness = new Harness();

        // A provider that does not implement the pre-dial seam at all.
        harness.VoiceProviderResolver
            .Setup(resolver => resolver.Get("Telnyx"))
            .Returns(new Mock<IContactCenterVoiceProvider>().Object);

        // Act
        var dialed = await harness.Coordinator.PreDialAsync("r1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(dialed);
        Assert.Null(await harness.Store.FindAsync("r1", TestContext.Current.CancellationToken));
        Assert.Null(await harness.Coordinator.GetForAcceptAsync("r1", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PreDialAsync_WhenTheAgentHasNoClientThatCanHoldTheLeg_LeavesTheAcceptToConnectTheAgent()
    {
        // Arrange
        var harness = new Harness();
        harness.PreDialProvider
            .Setup(provider => provider.PreDialAgentAsync(It.IsAny<ContactCenterAgentPreDialRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContactCenterVoiceProviderResult.Failure("Telnyx", "agent_endpoint_unsupported", "No live credential."));

        // Act
        var dialed = await harness.Coordinator.PreDialAsync("r1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(dialed);
        Assert.Null(await harness.Coordinator.GetForAcceptAsync("r1", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PreDialAsync_WhenTheOfferIsNoLongerPending_DoesNotRingTheAgent()
    {
        // Arrange
        var harness = new Harness();
        harness.Reservation.RestorePersistedStatus(ReservationStatus.Rejected);

        // Act
        var dialed = await harness.Coordinator.PreDialAsync("r1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(dialed);
        harness.PreDialProvider.Verify(
            provider => provider.PreDialAgentAsync(It.IsAny<ContactCenterAgentPreDialRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PreDialAsync_WhenTheOfferEndsWhileTheLegIsBeingPlaced_HangsTheLegUp()
    {
        // Arrange
        var harness = new Harness();
        harness.PreDialProvider
            .Setup(provider => provider.PreDialAgentAsync(It.IsAny<ContactCenterAgentPreDialRequest>(), It.IsAny<CancellationToken>()))
            .Callback(() => harness.Reservation.RestorePersistedStatus(ReservationStatus.Expired))
            .ReturnsAsync(Dialed("leg-1"));

        // Act
        var dialed = await harness.Coordinator.PreDialAsync("r1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(dialed);
        harness.PreDialProvider.Verify(provider => provider.HangupPreDialedAgentAsync("leg-1", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Null(await harness.Store.FindAsync("r1", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheLegIsJoined_WhenTheAgentAnswersBeforeTheAcceptReadiesTheCaller()
    {
        // Arrange
        var harness = await Harness.WithPreDialedLegAsync();

        // Act
        // The agent's browser answers the moment they click, ahead of the accept.
        await harness.Coordinator.OnAgentLegAnsweredAsync("Telnyx", "r1", "leg-1", TestContext.Current.CancellationToken);

        // Assert: nothing is joined to a caller for an offer that is not accepted yet.
        harness.PreDialProvider.Verify(
            provider => provider.BridgePreDialedAgentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // Act
        harness.Reservation.RestorePersistedStatus(ReservationStatus.Accepted);
        var bridged = await harness.Coordinator.OnCallerReadyAsync("r1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(bridged);
        harness.PreDialProvider.Verify(provider => provider.BridgePreDialedAgentAsync("call-1", "leg-1", It.IsAny<CancellationToken>()), Times.Once);

        // The answer command records the topology from this result, so the webhook half does not also.
        harness.AgentLegFailureService.Verify(
            service => service.RecordAnsweredAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TheLegIsJoined_WhenTheAcceptReadiesTheCallerBeforeTheAgentAnswers()
    {
        // Arrange
        var harness = await Harness.WithPreDialedLegAsync();
        harness.Reservation.RestorePersistedStatus(ReservationStatus.Accepted);

        // Act
        var bridgedOnAccept = await harness.Coordinator.OnCallerReadyAsync("r1", TestContext.Current.CancellationToken);
        await harness.Coordinator.OnAgentLegAnsweredAsync("Telnyx", "r1", "leg-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(bridgedOnAccept);
        harness.PreDialProvider.Verify(provider => provider.BridgePreDialedAgentAsync("call-1", "leg-1", It.IsAny<CancellationToken>()), Times.Once);
        harness.AgentLegFailureService.Verify(
            service => service.RecordAnsweredAsync("Telnyx", "call-1", "leg-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TheLegIsJoinedOnlyOnce_WhenBothHalvesAreRedelivered()
    {
        // Arrange
        var harness = await Harness.WithPreDialedLegAsync();
        harness.Reservation.RestorePersistedStatus(ReservationStatus.Accepted);

        // Act
        await harness.Coordinator.OnCallerReadyAsync("r1", TestContext.Current.CancellationToken);
        await harness.Coordinator.OnAgentLegAnsweredAsync("Telnyx", "r1", "leg-1", TestContext.Current.CancellationToken);
        await harness.Coordinator.OnAgentLegAnsweredAsync("Telnyx", "r1", "leg-1", TestContext.Current.CancellationToken);
        var bridgedAgain = await harness.Coordinator.OnCallerReadyAsync("r1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(bridgedAgain);
        harness.PreDialProvider.Verify(
            provider => provider.BridgePreDialedAgentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Null(await harness.Coordinator.GetForAcceptAsync("r1", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(ReservationStatus.Rejected)]
    [InlineData(ReservationStatus.Expired)]
    [InlineData(ReservationStatus.Canceled)]
    public async Task OnAgentLegAnsweredAsync_WhenTheOfferEndedWithoutBeingAccepted_HangsUpAndNeverJoins(ReservationStatus status)
    {
        // Arrange
        var harness = await Harness.WithPreDialedLegAsync();
        harness.Reservation.RestorePersistedStatus(status);

        // Act
        await harness.Coordinator.OnAgentLegAnsweredAsync("Telnyx", "r1", "leg-1", TestContext.Current.CancellationToken);

        // Assert
        harness.PreDialProvider.Verify(provider => provider.HangupPreDialedAgentAsync("leg-1", It.IsAny<CancellationToken>()), Times.Once);
        harness.PreDialProvider.Verify(
            provider => provider.BridgePreDialedAgentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Null(await harness.Store.FindAsync("r1", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task OnAgentLegAnsweredAsync_WhenTheOfferWentToAnotherAgent_HangsUpAndNeverJoins()
    {
        // Arrange
        var harness = await Harness.WithPreDialedLegAsync();
        harness.Reservation.AgentId = "a2";
        harness.Reservation.RestorePersistedStatus(ReservationStatus.Accepted);

        // Act
        await harness.Coordinator.OnAgentLegAnsweredAsync("Telnyx", "r1", "leg-1", TestContext.Current.CancellationToken);

        // Assert
        harness.PreDialProvider.Verify(provider => provider.HangupPreDialedAgentAsync("leg-1", It.IsAny<CancellationToken>()), Times.Once);
        harness.PreDialProvider.Verify(
            provider => provider.BridgePreDialedAgentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OnAgentLegAnsweredAsync_WhenNothingTracksTheLegAnyMore_HangsItUp()
    {
        // Arrange
        // The offer was released (and its state forgotten) before the browser's answer arrived.
        var harness = new Harness();

        // Act
        await harness.Coordinator.OnAgentLegAnsweredAsync("Telnyx", "r1", "leg-stale", TestContext.Current.CancellationToken);

        // Assert
        harness.PreDialProvider.Verify(provider => provider.HangupPreDialedAgentAsync("leg-stale", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReleaseAsync_HangsUpALegThatWasNotJoined()
    {
        // Arrange
        var harness = await Harness.WithPreDialedLegAsync();

        // Act
        await harness.Coordinator.ReleaseAsync("r1", TestContext.Current.CancellationToken);

        // Assert
        harness.PreDialProvider.Verify(provider => provider.HangupPreDialedAgentAsync("leg-1", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Null(await harness.Store.FindAsync("r1", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReleaseAsync_LeavesAJoinedLegAlone()
    {
        // Arrange
        var harness = await Harness.WithPreDialedLegAsync();
        harness.Reservation.RestorePersistedStatus(ReservationStatus.Accepted);
        await harness.Coordinator.OnAgentLegAnsweredAsync("Telnyx", "r1", "leg-1", TestContext.Current.CancellationToken);
        await harness.Coordinator.OnCallerReadyAsync("r1", TestContext.Current.CancellationToken);

        // Act
        await harness.Coordinator.ReleaseAsync("r1", TestContext.Current.CancellationToken);
        await harness.Coordinator.ReleaseForInteractionAsync("int1", TestContext.Current.CancellationToken);
        await harness.Coordinator.ReleaseForAgentAsync("a1", TestContext.Current.CancellationToken);

        // Assert
        harness.PreDialProvider.Verify(provider => provider.HangupPreDialedAgentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReleaseForInteractionAsync_WhenTheCallerHangsUpWhileTheOfferRings_HangsUpTheLeg()
    {
        // Arrange
        var harness = await Harness.WithPreDialedLegAsync();

        // Act
        await harness.Coordinator.ReleaseForInteractionAsync("int1", TestContext.Current.CancellationToken);

        // Assert
        harness.PreDialProvider.Verify(provider => provider.HangupPreDialedAgentAsync("leg-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReleaseForAgentAsync_WhenTheAgentSignsOut_HangsUpTheLeg()
    {
        // Arrange
        var harness = await Harness.WithPreDialedLegAsync();

        // Act
        await harness.Coordinator.ReleaseForAgentAsync("a1", TestContext.Current.CancellationToken);

        // Assert
        harness.PreDialProvider.Verify(provider => provider.HangupPreDialedAgentAsync("leg-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OnAgentLegEndedAsync_WhenTheAcceptedOffersLegDiesBeforeItIsJoined_FailsTheCall()
    {
        // Arrange
        var harness = await Harness.WithPreDialedLegAsync();
        harness.Reservation.RestorePersistedStatus(ReservationStatus.Accepted);

        // Act
        await harness.Coordinator.OnAgentLegEndedAsync("Telnyx", "r1", "leg-1", HangupCause.Rejected, TestContext.Current.CancellationToken);

        // Assert
        harness.AgentLegFailureService.Verify(
            service => service.FailAsync("Telnyx", "call-1", HangupCause.Rejected, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnAgentLegEndedAsync_WhileTheOfferIsStillRinging_OnlyForgetsTheLeg()
    {
        // Arrange
        // The agent closed the page holding the leg, or it rang out. The accept, if it comes, connects the agent
        // the ordinary way.
        var harness = await Harness.WithPreDialedLegAsync();

        // Act
        await harness.Coordinator.OnAgentLegEndedAsync("Telnyx", "r1", "leg-1", HangupCause.NoAnswer, TestContext.Current.CancellationToken);

        // Assert
        harness.AgentLegFailureService.Verify(
            service => service.FailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<HangupCause?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Null(await harness.Coordinator.GetForAcceptAsync("r1", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ABridgeTheProviderRefuses_HangsUpTheLegAndFailsTheCall()
    {
        // Arrange
        var harness = await Harness.WithPreDialedLegAsync();
        harness.Reservation.RestorePersistedStatus(ReservationStatus.Accepted);
        harness.PreDialProvider
            .Setup(provider => provider.BridgePreDialedAgentAsync("call-1", "leg-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContactCenterVoiceProviderResult.Failure("Telnyx", "bridge_failed", "Refused."));
        await harness.Coordinator.OnAgentLegAnsweredAsync("Telnyx", "r1", "leg-1", TestContext.Current.CancellationToken);

        // Act
        var bridged = await harness.Coordinator.OnCallerReadyAsync("r1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(bridged);
        harness.PreDialProvider.Verify(provider => provider.HangupPreDialedAgentAsync("leg-1", It.IsAny<CancellationToken>()), Times.Once);
        harness.AgentLegFailureService.Verify(
            service => service.FailAsync("Telnyx", "call-1", HangupCause.Failed, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PreDialAsync_WhenTurnedOff_NeverRingsTheAgent()
    {
        // Arrange
        var harness = new Harness(options => options.AgentPreDialEnabled = false);

        // Act
        var dialed = await harness.Coordinator.PreDialAsync("r1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(dialed);
        harness.PreDialProvider.Verify(
            provider => provider.PreDialAgentAsync(It.IsAny<ContactCenterAgentPreDialRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(ContactCenterConstants.Events.AgentReleased)]
    [InlineData(ContactCenterConstants.Events.OfferDeclined)]
    public async Task EventHandler_ReleasesTheLeg_WhenTheOfferEnds(string eventType)
    {
        // Arrange
        var coordinator = new Mock<IAgentPreDialCoordinator>();
        var handler = new AgentPreDialEventHandler(new Lazy<IAgentPreDialCoordinator>(() => coordinator.Object));

        // Act
        await handler.HandleAsync(new InteractionEvent { EventType = eventType, AggregateId = "r1" }, TestContext.Current.CancellationToken);

        // Assert
        coordinator.Verify(value => value.ReleaseAsync("r1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EventHandler_PreDials_WhenTheAgentIsReserved()
    {
        // Arrange
        var coordinator = new Mock<IAgentPreDialCoordinator>();
        var handler = new AgentPreDialEventHandler(new Lazy<IAgentPreDialCoordinator>(() => coordinator.Object));

        // Act
        await handler.HandleAsync(
            new InteractionEvent { EventType = ContactCenterConstants.Events.AgentReserved, AggregateId = "r1" },
            TestContext.Current.CancellationToken);

        // Assert
        coordinator.Verify(value => value.PreDialAsync("r1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EventHandler_ReleasesTheLeg_WhenTheCallerHangsUpOrTheAgentSignsOut()
    {
        // Arrange
        var coordinator = new Mock<IAgentPreDialCoordinator>();
        var handler = new AgentPreDialEventHandler(new Lazy<IAgentPreDialCoordinator>(() => coordinator.Object));

        // Act
        await handler.HandleAsync(
            new InteractionEvent { EventType = ContactCenterConstants.Events.CallEnded, InteractionId = "int1", AggregateId = "int1" },
            TestContext.Current.CancellationToken);
        await handler.HandleAsync(
            new InteractionEvent { EventType = ContactCenterConstants.Events.AgentSignedOut, AggregateId = "a1" },
            TestContext.Current.CancellationToken);

        // Assert
        coordinator.Verify(value => value.ReleaseForInteractionAsync("int1", It.IsAny<CancellationToken>()), Times.Once);
        coordinator.Verify(value => value.ReleaseForAgentAsync("a1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RedialAgentLegAsync_RingsTheNewEndpointForTheRestOfTheRingWindow_AndTracksTheNewLegInstead()
    {
        // Arrange
        // The agent's phone had reopened on a fresh credential, so the leg to its old one was refused as unavailable.
        var harness = await Harness.WithPreDialedLegAsync();
        ContactCenterAgentPreDialRequest? request = null;
        harness.PreDialProvider
            .Setup(provider => provider.PreDialAgentAsync(It.IsAny<ContactCenterAgentPreDialRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ContactCenterAgentPreDialRequest, CancellationToken>((value, _) => request = value)
            .ReturnsAsync(Dialed("leg-2"));

        // Act
        var redialed = await harness.Coordinator.RedialAgentLegAsync("Telnyx", "r1", "leg-1", "sip:new@example", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(redialed);
        Assert.NotNull(request);
        Assert.Equal("sip:new@example", request!.AgentEndpoint);
        Assert.Equal("leg-1", request.ReplacesAgentLegId);
        Assert.Equal("u1", request.AgentUserId);
        Assert.Equal(25, request.TimeoutSeconds);

        var leg = await harness.Store.FindAsync("r1", TestContext.Current.CancellationToken);
        Assert.Equal("leg-2", leg!.AgentLegId);

        // The new leg is the offer's now: answering it is not mistaken for a stray leg and hung up.
        await harness.Coordinator.OnAgentLegAnsweredAsync("Telnyx", "r1", "leg-2", TestContext.Current.CancellationToken);
        harness.PreDialProvider.Verify(provider => provider.HangupPreDialedAgentAsync("leg-2", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RedialAgentLegAsync_WhenTheOfferIsNoLongerTheAgents_RingsNothing()
    {
        // Arrange
        var harness = await Harness.WithPreDialedLegAsync();
        harness.Reservation.RestorePersistedStatus(ReservationStatus.Expired);

        // Act
        var redialed = await harness.Coordinator.RedialAgentLegAsync("Telnyx", "r1", "leg-1", "sip:new@example", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(redialed);
        harness.PreDialProvider.Verify(
            provider => provider.PreDialAgentAsync(It.Is<ContactCenterAgentPreDialRequest>(value => value.ReplacesAgentLegId != null), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RedialAgentLegAsync_ForALegTheOfferNoLongerTracks_RingsNothing()
    {
        // Arrange
        // A late or repeated webhook for a leg already replaced.
        var harness = await Harness.WithPreDialedLegAsync();

        // Act
        var redialed = await harness.Coordinator.RedialAgentLegAsync("Telnyx", "r1", "leg-0", "sip:new@example", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(redialed);
        Assert.Equal("leg-1", (await harness.Store.FindAsync("r1", TestContext.Current.CancellationToken))!.AgentLegId);
    }

    private static ContactCenterVoiceProviderResult Dialed(string legId)
        => new()
        {
            Succeeded = true,
            ProviderName = "Telnyx",
            ProviderCallId = "call-1",
            ProviderLegId = legId,
            ProviderLegState = VoiceCallState.Dialing,
        };

    private sealed class Harness
    {
        public Harness(Action<ContactCenterCoordinationOptions>? configure = null)
        {
            var clock = new Mock<IClock>();
            clock.SetupGet(value => value.UtcNow).Returns(_now);

            Store = new DistributedCacheAgentPreDialLegStore(
                new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
                clock.Object,
                new ShellSettings { Name = "Default" });

            ReservationManager
                .Setup(manager => manager.FindByIdAsync("r1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Reservation);

            InteractionManager
                .Setup(manager => manager.FindByActivityIdAsync("act1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Interaction
                {
                    ItemId = "int1",
                    ActivityItemId = "act1",
                    ProviderName = "Telnyx",
                    ProviderInteractionId = "call-1",
                    Direction = InteractionDirection.Inbound,
                }.RestorePersistedStatus(InteractionStatus.Ringing));

            AgentManager
                .Setup(manager => manager.FindByIdAsync("a1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AgentProfile { ItemId = "a1", UserId = "u1" });

            Provider.SetupGet(provider => provider.TechnicalName).Returns("Telnyx");
            Provider.SetupGet(provider => provider.DeliveryModel).Returns(VoiceProviderDeliveryModel.ServerSideAcd);
            PreDialProvider = Provider.As<IContactCenterVoiceAgentPreDialProvider>();
            PreDialProvider
                .Setup(provider => provider.BridgePreDialedAgentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ContactCenterVoiceProviderResult { Succeeded = true, ProviderName = "Telnyx" });
            VoiceProviderResolver.Setup(resolver => resolver.Get("Telnyx")).Returns(Provider.Object);

            var distributedLock = new Mock<IDistributedLock>();
            distributedLock
                .Setup(value => value.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
                .ReturnsAsync(() => (new Mock<ILocker>().Object, true));

            var options = new ContactCenterCoordinationOptions();
            configure?.Invoke(options);

            Coordinator = new AgentPreDialCoordinator(
                Store,
                VoiceProviderResolver.Object,
                InteractionManager.Object,
                AgentManager.Object,
                AgentLegFailureService.Object,
                new StubScopeExecutor(ReservationManager.Object),
                distributedLock.Object,
                clock.Object,
                Options.Create(options),
                NullLogger<AgentPreDialCoordinator>.Instance);
        }

        public ActivityReservation Reservation { get; } = new ActivityReservation
        {
            ItemId = "r1",
            AgentId = "a1",
            ActivityItemId = "act1",
            QueueId = "q1",
            ExpiresUtc = _now.AddSeconds(20),
        }.RestorePersistedStatus(ReservationStatus.Pending);

        public DistributedCacheAgentPreDialLegStore Store { get; }

        public Mock<IActivityReservationManager> ReservationManager { get; } = new();

        public Mock<IInteractionManager> InteractionManager { get; } = new();

        public Mock<IAgentProfileManager> AgentManager { get; } = new();

        public Mock<IContactCenterAgentLegFailureService> AgentLegFailureService { get; } = new();

        public Mock<IContactCenterVoiceProviderResolver> VoiceProviderResolver { get; } = new();

        public Mock<IContactCenterVoiceProvider> Provider { get; } = new();

        public Mock<IContactCenterVoiceAgentPreDialProvider> PreDialProvider { get; }

        public AgentPreDialCoordinator Coordinator { get; }

        public static async Task<Harness> WithPreDialedLegAsync()
        {
            var harness = new Harness();
            harness.PreDialProvider
                .Setup(provider => provider.PreDialAgentAsync(It.IsAny<ContactCenterAgentPreDialRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Dialed("leg-1"));

            Assert.True(await harness.Coordinator.PreDialAsync("r1", TestContext.Current.CancellationToken));

            return harness;
        }
    }

    private sealed class StubScopeExecutor : IContactCenterScopeExecutor
    {
        private readonly object _context;

        public StubScopeExecutor(object context)
        {
            _context = context;
        }

        public Task ExecuteAsync<TContext>(Func<TContext, Task> operation)
            where TContext : notnull
            => operation((TContext)_context);

        public Task ExecuteAsync(Func<IServiceProvider, Task> operation)
            => throw new NotSupportedException();

        public bool ScheduleAfterCommit<TContext>(Func<TContext, Task> operation)
            where TContext : notnull
            => false;

        public bool ScheduleAfterCommit(Func<Task> operation)
            => false;
    }
}

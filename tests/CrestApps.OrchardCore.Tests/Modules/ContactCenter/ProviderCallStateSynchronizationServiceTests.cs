using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ProviderCallStateSynchronizationServiceTests
{
    private static readonly DateTime _now = new(2026, 7, 11, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RefreshInteractionAsync_WhenProviderNoLongerHasCall_EndsLocalInteraction()
    {
        // Arrange
        var interaction = CreateInteraction();
        ProviderVoiceEvent providerEvent = null;
        var eventService = new Mock<IProviderVoiceEventService>();
        eventService
            .Setup(service => service.IngestAsync(It.IsAny<ProviderVoiceEvent>(), It.IsAny<CancellationToken>()))
            .Callback<ProviderVoiceEvent, CancellationToken>((value, _) => providerEvent = value)
            .ReturnsAsync(new CallSession());
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.FindByProviderInteractionIdAsync("provider-1", "call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Interaction
            {
                ItemId = "interaction-1",
                ProviderName = "provider-1",
                ProviderInteractionId = "call-1",
            }.RestorePersistedStatus(InteractionStatus.Ended));
        var service = CreateService(
            interactionManager,
            new Mock<ICallSessionManager>(),
            eventService,
            new TelephonyCallLookupResult
            {
                Succeeded = true,
                Found = false,
            });

        // Act
        var refreshed = await service.RefreshInteractionAsync(interaction, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(InteractionStatus.Ended, refreshed.Status);
        Assert.NotNull(providerEvent);
        Assert.Equal(VoiceCallState.Ended, providerEvent.State);
        Assert.Equal("reconcile-missing:provider-1:call-1:ended", providerEvent.IdempotencyKey);
    }

    [Fact]
    public async Task RefreshInteractionAsync_WhenStoredProviderIsMissing_ReconcilesThroughDefaultProvider()
    {
        // Arrange
        var interaction = CreateInteraction();
        interaction.ProviderName = "stale-provider";
        ProviderVoiceEvent providerEvent = null;
        var eventService = new Mock<IProviderVoiceEventService>();
        eventService
            .Setup(service => service.IngestAsync(It.IsAny<ProviderVoiceEvent>(), It.IsAny<CancellationToken>()))
            .Callback<ProviderVoiceEvent, CancellationToken>((value, _) => providerEvent = value)
            .ReturnsAsync(new CallSession());
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.FindByProviderInteractionIdAsync("Default Asterisk", "call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Interaction
            {
                ItemId = "interaction-1",
                ProviderName = "Default Asterisk",
                ProviderInteractionId = "call-1",
            }.RestorePersistedStatus(InteractionStatus.Ended));
        var provider = new Mock<ITelephonyProvider>();
        provider.SetupGet(value => value.Name)
            .Returns(new Microsoft.Extensions.Localization.LocalizedString("Default Asterisk", "Default Asterisk"));
        provider
            .As<ITelephonyCallStateProvider>()
            .Setup(value => value.GetCallStateAsync("call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelephonyCallLookupResult
            {
                Succeeded = true,
                Found = false,
            });
        var resolver = new Mock<ITelephonyProviderResolver>();
        resolver.Setup(value => value.GetAsync("stale-provider")).ReturnsAsync((ITelephonyProvider)null);
        resolver.Setup(value => value.GetAsync(null)).ReturnsAsync(provider.Object);
        var service = CreateService(interactionManager, new Mock<ICallSessionManager>(), eventService, resolver);

        // Act
        var refreshed = await service.RefreshInteractionAsync(interaction, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(InteractionStatus.Ended, refreshed.Status);
        Assert.NotNull(providerEvent);
        Assert.Equal("Default Asterisk", providerEvent.ProviderName);
        Assert.Equal("reconcile-missing:Default Asterisk:call-1:ended", providerEvent.IdempotencyKey);
    }

    [Fact]
    public async Task RefreshInteractionAsync_WhenProviderReportsHeldAndMuted_PropagatesGranularState()
    {
        // Arrange
        var interaction = CreateInteraction();
        ProviderVoiceEvent providerEvent = null;
        var eventService = new Mock<IProviderVoiceEventService>();
        eventService
            .Setup(service => service.IngestAsync(It.IsAny<ProviderVoiceEvent>(), It.IsAny<CancellationToken>()))
            .Callback<ProviderVoiceEvent, CancellationToken>((value, _) => providerEvent = value)
            .ReturnsAsync(new CallSession());
        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager
            .Setup(manager => manager.FindByInteractionIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CallSession
            {
                InteractionId = "interaction-1",
                ProviderCallId = "call-1",
            }.RestorePersistedState(VoiceCallState.Connected));
        var service = CreateService(
            new Mock<IInteractionManager>(),
            callSessionManager,
            eventService,
            new TelephonyCallLookupResult
            {
                Succeeded = true,
                Found = true,
                Call = new TelephonyCall
                {
                    CallId = "call-1",
                    State = CallState.Connected,
                    IsOnHold = true,
                    IsMuted = true,
                },
            });

        // Act
        await service.RefreshInteractionAsync(interaction, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(providerEvent);
        Assert.Equal(VoiceCallState.OnHold, providerEvent.State);
        Assert.True(providerEvent.IsMuted);
        Assert.Contains(":OnHold:True:True", providerEvent.IdempotencyKey, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RefreshInteractionAsync_WhenProviderAndSessionMatch_DoesNotRepublishState()
    {
        // Arrange
        var interaction = CreateInteraction();
        var eventService = new Mock<IProviderVoiceEventService>();
        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager
            .Setup(manager => manager.FindByInteractionIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CallSession
            {
                InteractionId = "interaction-1",
                ProviderCallId = "call-1",
                IsOnHold = true,
                IsMuted = true,
            }.RestorePersistedState(VoiceCallState.OnHold));
        var service = CreateService(
            new Mock<IInteractionManager>(),
            callSessionManager,
            eventService,
            new TelephonyCallLookupResult
            {
                Succeeded = true,
                Found = true,
                Call = new TelephonyCall
                {
                    CallId = "call-1",
                    State = CallState.OnHold,
                    IsOnHold = true,
                    IsMuted = true,
                },
            });

        // Act
        var refreshed = await service.RefreshInteractionAsync(interaction, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(interaction, refreshed);
        eventService.Verify(
            service => service.IngestAsync(It.IsAny<ProviderVoiceEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // Bug: a caller handed from the AI to a queue is on a live, answered leg the whole time they wait -- hearing hold
    // music, while an agent's phone rings for them. The provider can only say that leg is alive, which the sweep read
    // as "connected": it created the call's session straight into Connected, stamped the interaction answered, and the
    // ringing agent's phone showed a call in progress that nobody had accepted, until the offer expired under it.
    [Theory]
    [InlineData(InteractionStatus.Created)]
    [InlineData(InteractionStatus.Ringing)]
    public async Task RefreshInteractionAsync_WhenNobodyAnsweredYet_DoesNotReadALiveCallAsConnected(InteractionStatus status)
    {
        // Arrange
        var interaction = CreateInteraction();
        interaction.RestorePersistedStatus(status);
        var eventService = new Mock<IProviderVoiceEventService>();
        var service = CreateService(
            new Mock<IInteractionManager>(),
            new Mock<ICallSessionManager>(),
            eventService,
            new TelephonyCallLookupResult
            {
                Succeeded = true,
                Found = true,
                Call = new TelephonyCall
                {
                    CallId = "call-1",
                    State = CallState.Connected,
                },
            });

        // Act
        var refreshed = await service.RefreshInteractionAsync(interaction, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(interaction, refreshed);
        Assert.Equal(status, refreshed.Status);
        eventService.Verify(
            value => value.IngestAsync(It.IsAny<ProviderVoiceEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // The same leg, with the call's session already opened by the offer (ringing): still nobody on it.
    [Fact]
    public async Task RefreshInteractionAsync_WhenTheSessionIsStillRinging_DoesNotReadALiveCallAsConnected()
    {
        // Arrange
        var interaction = CreateInteraction();
        interaction.RestorePersistedStatus(InteractionStatus.Ringing);
        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager
            .Setup(manager => manager.FindByInteractionIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CallSession
            {
                InteractionId = "interaction-1",
                ProviderCallId = "call-1",
            }.RestorePersistedState(VoiceCallState.Ringing));
        var eventService = new Mock<IProviderVoiceEventService>();
        var service = CreateService(
            new Mock<IInteractionManager>(),
            callSessionManager,
            eventService,
            new TelephonyCallLookupResult
            {
                Succeeded = true,
                Found = true,
                Call = new TelephonyCall
                {
                    CallId = "call-1",
                    State = CallState.OnHold,
                    IsOnHold = true,
                },
            });

        // Act
        await service.RefreshInteractionAsync(interaction, TestContext.Current.CancellationToken);

        // Assert
        eventService.Verify(
            value => value.IngestAsync(It.IsAny<ProviderVoiceEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // A call that has not been answered can still end: the sweep is how a missed hang-up is caught.
    [Fact]
    public async Task RefreshInteractionAsync_WhenNobodyAnsweredAndTheProviderSaysTheCallEnded_StillEndsIt()
    {
        // Arrange
        var interaction = CreateInteraction();
        interaction.RestorePersistedStatus(InteractionStatus.Ringing);
        ProviderVoiceEvent providerEvent = null;
        var eventService = new Mock<IProviderVoiceEventService>();
        eventService
            .Setup(service => service.IngestAsync(It.IsAny<ProviderVoiceEvent>(), It.IsAny<CancellationToken>()))
            .Callback<ProviderVoiceEvent, CancellationToken>((value, _) => providerEvent = value)
            .ReturnsAsync(new CallSession());
        var service = CreateService(
            new Mock<IInteractionManager>(),
            new Mock<ICallSessionManager>(),
            eventService,
            new TelephonyCallLookupResult
            {
                Succeeded = true,
                Found = true,
                Call = new TelephonyCall
                {
                    CallId = "call-1",
                    State = CallState.Disconnected,
                },
            });

        // Act
        await service.RefreshInteractionAsync(interaction, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(providerEvent);
        Assert.Equal(VoiceCallState.Ended, providerEvent.State);
    }

    // An answered call whose session never recorded it (a restart lost the event) is still repaired from the provider.
    [Fact]
    public async Task RefreshInteractionAsync_WhenTheInteractionWasAnsweredButHasNoSession_ReconcilesTheLiveCall()
    {
        // Arrange
        var interaction = CreateInteraction();
        ProviderVoiceEvent providerEvent = null;
        var eventService = new Mock<IProviderVoiceEventService>();
        eventService
            .Setup(service => service.IngestAsync(It.IsAny<ProviderVoiceEvent>(), It.IsAny<CancellationToken>()))
            .Callback<ProviderVoiceEvent, CancellationToken>((value, _) => providerEvent = value)
            .ReturnsAsync(new CallSession());
        var service = CreateService(
            new Mock<IInteractionManager>(),
            new Mock<ICallSessionManager>(),
            eventService,
            new TelephonyCallLookupResult
            {
                Succeeded = true,
                Found = true,
                Call = new TelephonyCall
                {
                    CallId = "call-1",
                    State = CallState.Connected,
                },
            });

        // Act
        await service.RefreshInteractionAsync(interaction, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(providerEvent);
        Assert.Equal(VoiceCallState.Connected, providerEvent.State);
    }

    [Fact]
    public async Task RefreshInteractionAsync_WhenTerminalSessionHasNonTerminalInteraction_RepairsInteractionAndOfferState()
    {
        // Arrange
        var interaction = CreateInteraction();
        interaction.RestorePersistedStatus(InteractionStatus.Ringing);
        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager
            .Setup(manager => manager.FindByInteractionIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CallSession
            {
                ItemId = "session-1",
                InteractionId = "interaction-1",
                ProviderCallId = "call-1",
                StartedUtc = _now.AddMinutes(-2),
                EndedUtc = _now.AddMinutes(-1),
            }.RestorePersistedState(VoiceCallState.Ended));
        var interactionManager = new Mock<IInteractionManager>();
        var eventService = new Mock<IProviderVoiceEventService>();
        var offerSynchronizationService = new Mock<IProviderVoiceOfferSynchronizationService>();
        var resolver = new Mock<ITelephonyProviderResolver>();
        var service = CreateService(
            interactionManager,
            callSessionManager,
            eventService,
            offerSynchronizationService,
            resolver);

        // Act
        var refreshed = await service.RefreshInteractionAsync(interaction, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(InteractionStatus.Ended, refreshed.Status);
        Assert.Equal(_now.AddMinutes(-2), refreshed.StartedUtc);
        Assert.Equal(_now.AddMinutes(-1), refreshed.EndedUtc);
        interactionManager.Verify(
            manager => manager.UpdateAsync(
                It.Is<Interaction>(value => value.Status == InteractionStatus.Ended),
                It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        offerSynchronizationService.Verify(
            service => service.ReconcileEndedOfferAsync("interaction-1", It.IsAny<CancellationToken>()),
            Times.Once);
        eventService.Verify(
            service => service.IngestAsync(It.IsAny<ProviderVoiceEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
        resolver.Verify(
            value => value.GetAsync(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task RefreshInteractionAsync_WhenTheCallerCancelledBeforeAnswer_RepairsTheInteractionToEndedNotFailed()
    {
        // Arrange
        var interaction = CreateInteraction();
        interaction.RestorePersistedStatus(InteractionStatus.Ringing);
        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager
            .Setup(manager => manager.FindByInteractionIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CallSession
            {
                ItemId = "session-1",
                InteractionId = "interaction-1",
                ProviderCallId = "call-1",
                StartedUtc = _now.AddMinutes(-2),
                EndedUtc = _now.AddMinutes(-1),
            }.RestorePersistedState(VoiceCallState.Canceled));
        var service = CreateService(
            new Mock<IInteractionManager>(),
            callSessionManager,
            new Mock<IProviderVoiceEventService>(),
            new Mock<IProviderVoiceOfferSynchronizationService>(),
            new Mock<ITelephonyProviderResolver>());

        // Act
        var refreshed = await service.RefreshInteractionAsync(interaction, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(InteractionStatus.Ended, refreshed.Status);
    }

    [Fact]
    public async Task RefreshInteractionAsync_WhenAnInteractionAlreadySettledOnAnotherEnding_KeepsItAndStillReconcilesTheOffer()
    {
        // Arrange
        // Interactions stored before a caller's cancel meant "ended" settled as failed. A settled status has no way
        // out, so repairing it towards the newer mapping would throw on every pass over that history.
        var interaction = CreateInteraction();
        interaction.RestorePersistedStatus(InteractionStatus.Failed);
        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager
            .Setup(manager => manager.FindByInteractionIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CallSession
            {
                ItemId = "session-1",
                InteractionId = "interaction-1",
                ProviderCallId = "call-1",
                EndedUtc = _now.AddMinutes(-1),
            }.RestorePersistedState(VoiceCallState.Canceled));
        var interactionManager = new Mock<IInteractionManager>();
        var offerSynchronizationService = new Mock<IProviderVoiceOfferSynchronizationService>();
        var service = CreateService(
            interactionManager,
            callSessionManager,
            new Mock<IProviderVoiceEventService>(),
            offerSynchronizationService,
            new Mock<ITelephonyProviderResolver>());

        // Act
        var refreshed = await service.RefreshInteractionAsync(interaction, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(InteractionStatus.Failed, refreshed.Status);
        interactionManager.Verify(
            manager => manager.UpdateAsync(It.IsAny<Interaction>(), It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()),
            Times.Never);
        offerSynchronizationService.Verify(
            value => value.ReconcileEndedOfferAsync("interaction-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ReconcileProviderStateAsync_PerformsProviderReconciliation()
    {
        // Arrange
        var synchronizationService = new Mock<IProviderCallStateSynchronizationService>();
        synchronizationService
            .Setup(service => service.ReconcileActiveInteractionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        var participant = new ContactCenterVoiceLifecycleParticipant(
            synchronizationService.Object,
            new TestContactCenterFeatureWorkManager(),
            Options.Create(new ContactCenterFeatureLifecycleOptions()),
            NullLogger<ContactCenterVoiceLifecycleParticipant>.Instance);

        // Act
        await participant.ReconcileProviderStateAsync(TestContext.Current.CancellationToken);

        // Assert
        synchronizationService.Verify(
            service => service.ReconcileActiveInteractionsAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ReconcileProviderInteractionsAsync_OnlyLoadsInteractionsForReconnectingProvider()
    {
        // Arrange
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.GetActiveWithProviderCallIdAsync("provider-1", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var service = CreateService(
            interactionManager,
            new Mock<ICallSessionManager>(),
            new Mock<IProviderVoiceEventService>(),
            new TelephonyCallLookupResult());

        // Act
        var refreshed = await service.ReconcileProviderInteractionsAsync(
            "provider-1",
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, refreshed);
        interactionManager.Verify(
            manager => manager.GetActiveWithProviderCallIdAsync("provider-1", It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
        interactionManager.Verify(
            manager => manager.GetActiveWithProviderCallIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static ProviderCallStateSynchronizationService CreateService(
        Mock<IInteractionManager> interactionManager,
        Mock<ICallSessionManager> callSessionManager,
        Mock<IProviderVoiceEventService> eventService,
        TelephonyCallLookupResult lookup)
    {
        var provider = new Mock<ITelephonyProvider>();
        provider
            .As<ITelephonyCallStateProvider>()
            .Setup(value => value.GetCallStateAsync("call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(lookup);
        var resolver = new Mock<ITelephonyProviderResolver>();
        resolver.Setup(value => value.GetAsync("provider-1")).ReturnsAsync(provider.Object);

        return CreateService(interactionManager, callSessionManager, eventService, resolver);
    }

    private static ProviderCallStateSynchronizationService CreateService(
        Mock<IInteractionManager> interactionManager,
        Mock<ICallSessionManager> callSessionManager,
        Mock<IProviderVoiceEventService> eventService,
        Mock<ITelephonyProviderResolver> resolver)
    {
        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(value => value.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync((null, true));
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        return new ProviderCallStateSynchronizationService(
            interactionManager.Object,
            callSessionManager.Object,
            eventService.Object,
            new Mock<IProviderVoiceOfferSynchronizationService>().Object,
            resolver.Object,
            distributedLock.Object,
            clock.Object,
            NullLogger<ProviderCallStateSynchronizationService>.Instance);
    }

    private static ProviderCallStateSynchronizationService CreateService(
        Mock<IInteractionManager> interactionManager,
        Mock<ICallSessionManager> callSessionManager,
        Mock<IProviderVoiceEventService> eventService,
        Mock<IProviderVoiceOfferSynchronizationService> offerSynchronizationService,
        Mock<ITelephonyProviderResolver> resolver)
    {
        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(value => value.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync((null, true));
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        return new ProviderCallStateSynchronizationService(
            interactionManager.Object,
            callSessionManager.Object,
            eventService.Object,
            offerSynchronizationService.Object,
            resolver.Object,
            distributedLock.Object,
            clock.Object,
            NullLogger<ProviderCallStateSynchronizationService>.Instance);
    }

    private static Interaction CreateInteraction()
    {
        return new Interaction
        {
            ItemId = "interaction-1",
            ProviderName = "provider-1",
            ProviderInteractionId = "call-1",
        }.RestorePersistedStatus(InteractionStatus.Connected);
    }
}

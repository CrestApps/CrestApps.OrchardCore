using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
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
                Status = InteractionStatus.Ended,
            });
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
        Assert.Equal(ContactCenterCallState.Ended, providerEvent.State);
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
                Status = InteractionStatus.Ended,
            });
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
                State = ContactCenterCallState.Connected,
            });
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
        Assert.Equal(ContactCenterCallState.OnHold, providerEvent.State);
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
                State = ContactCenterCallState.OnHold,
                IsOnHold = true,
                IsMuted = true,
            });
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

    [Fact]
    public async Task RefreshInteractionAsync_WhenTerminalSessionHasNonTerminalInteraction_RepairsInteractionAndOfferState()
    {
        // Arrange
        var interaction = CreateInteraction();
        interaction.Status = InteractionStatus.Ringing;
        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager
            .Setup(manager => manager.FindByInteractionIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CallSession
            {
                ItemId = "session-1",
                InteractionId = "interaction-1",
                ProviderCallId = "call-1",
                State = ContactCenterCallState.Ended,
                StartedUtc = _now.AddMinutes(-2),
                EndedUtc = _now.AddMinutes(-1),
            });
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
    public async Task TenantActivation_PerformsImmediateProviderReconciliation()
    {
        // Arrange
        var synchronizationService = new Mock<IProviderCallStateSynchronizationService>();
        synchronizationService
            .Setup(service => service.ReconcileActiveInteractionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        using var ingressLimiter = new ProviderWebhookIngressLimiter(
            Options.Create(new ProviderWebhookIngressOptions()),
            Mock.Of<IClock>());
        var tenantEvents = new ContactCenterVoiceTenantEvents(
            synchronizationService.Object,
            ingressLimiter,
            NullLogger<ContactCenterVoiceTenantEvents>.Instance);

        // Act
        await tenantEvents.ReconcileAsync(TestContext.Current.CancellationToken);

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
            .Setup(manager => manager.ListActiveWithProviderCallIdAsync("provider-1", It.IsAny<CancellationToken>()))
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
            manager => manager.ListActiveWithProviderCallIdAsync("provider-1", It.IsAny<CancellationToken>()),
            Times.Once);
        interactionManager.Verify(
            manager => manager.ListActiveWithProviderCallIdAsync(It.IsAny<CancellationToken>()),
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
            Status = InteractionStatus.Connected,
        };
    }
}

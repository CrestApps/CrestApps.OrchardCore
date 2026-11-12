using CrestApps.OrchardCore.Asterisk.Services;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.SignalR.Core;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.Hubs;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;
using CrestApps.OrchardCore.Telnyx;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// SEC-FE2, the server half: the caller id of an incoming call is chosen by whoever places it, so it is cleaned where
/// a provider's event is mapped and where the ringing call is recorded, before anything stores or routes it. The call
/// history projection keeps an incoming call's caller as it was recorded, so cleaning only there missed the inbound
/// call the finding is about.
/// </summary>
public sealed class InboundCallerIdSanitizationTests
{
    private const string CraftedCaller = "555\" onmouseover=\"alert(1)";
    private const string CleanedCaller = "5551";
    private static readonly DateTime _now = new(2026, 11, 12, 18, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task TheIncomingCallDispatcher_RecordsARingingCall_WithItsCallerIdCleaned()
    {
        // Arrange
        TelephonyInteraction created = null;
        var store = new Mock<ITelephonyInteractionStore>();
        store
            .Setup(value => value.CreateAsync(It.IsAny<TelephonyInteraction>(), It.IsAny<CancellationToken>()))
            .Callback<TelephonyInteraction, CancellationToken>((interaction, _) => created = interaction)
            .Returns(Task.CompletedTask);

        // Act
        await CreateDispatcher(store.Object).DispatchAsync("user-1", InboundCall(CraftedCaller), TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(created);
        Assert.Equal(CleanedCaller, created.From);
    }

    [Fact]
    public async Task TheIncomingCallDispatcher_UpdatesARecordedCall_WithItsCallerIdCleaned()
    {
        // Arrange
        var existing = new TelephonyInteraction
        {
            InteractionId = "interaction-1",
            CallId = "call-1",
            ProviderName = "provider-1",
            UserId = "user-1",
            From = "+15550001000",
            Direction = CallDirection.Inbound,
        };
        var store = new Mock<ITelephonyInteractionStore>();
        store.Setup(value => value.FindByCallIdAsync("user-1", "call-1", It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        store.SetupRetryingUpdates(existing);

        // Act
        await CreateDispatcher(store.Object).DispatchAsync("user-1", InboundCall(CraftedCaller), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(CleanedCaller, existing.From);
    }

    // The whole inbound path: the dispatcher records the ringing call, then the provider's events update it. The
    // projection keeps an incoming call's caller, so what it keeps and shows is what the dispatcher stored.
    [Fact]
    public async Task AnInboundCall_KeptByTheCallHistoryProjection_IsShownWithItsCallerIdCleaned()
    {
        // Arrange
        TelephonyInteraction recorded = null;
        var store = new Mock<ITelephonyInteractionStore>();
        store
            .Setup(value => value.CreateAsync(It.IsAny<TelephonyInteraction>(), It.IsAny<CancellationToken>()))
            .Callback<TelephonyInteraction, CancellationToken>((interaction, _) => recorded = interaction)
            .Returns(Task.CompletedTask);
        await CreateDispatcher(store.Object).DispatchAsync("user-1", InboundCall(CraftedCaller), TestContext.Current.CancellationToken);
        store.SetupRetryingUpdates(recorded);

        TelephonyCall pushed = null;
        var client = new Mock<ITelephonyClient>();
        client.Setup(value => value.CallStateChanged(It.IsAny<TelephonyCall>())).Callback<TelephonyCall>(call => pushed = call).Returns(Task.CompletedTask);
        var handler = new TelephonyCallHistoryVoiceEventHandler(
            store.Object,
            HubContext(client.Object).Object,
            Clock(),
            NullLogger<TelephonyCallHistoryVoiceEventHandler>.Instance,
            new ShellSettings { Name = "TenantA" });

        // Act
        await handler.HandleAsync(new ProviderVoiceEvent
        {
            ProviderName = "provider-1",
            ProviderCallId = "call-1",
            State = VoiceCallState.Connected,
            FromAddress = "+17785550000",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(CleanedCaller, recorded.From);
        Assert.Equal(CleanedCaller, pushed.From);
    }

    [Fact]
    public void TheAsteriskMapper_CleansTheCallerIdOfAnInboundChannel()
    {
        // Arrange
        var payload = $$"""
            {
              "type": "StasisStart",
              "timestamp": "2026-11-12T18:00:00.000Z",
              "application": "crestapps-telephony",
              "args": [],
              "channel": {
                "id": "call-1",
                "state": "Ring",
                "caller": { "number": {{System.Text.Json.JsonSerializer.Serialize(CraftedCaller)}} },
                "dialplan": { "exten": "+17025550100" }
              }
            }
            """;

        // Act
        var mapped = AsteriskRealtimeVoiceEventMapper.TryMap("Asterisk", payload, out var voiceEvent);

        // Assert
        Assert.True(mapped);
        Assert.True(voiceEvent.IsInbound);
        Assert.Equal(CleanedCaller, voiceEvent.FromAddress);
        Assert.Equal(CleanedCaller, voiceEvent.CallerNumber);
    }

    [Fact]
    public async Task TheTelnyxInboundRouter_RoutesTheCallWithItsCallerIdCleaned()
    {
        // Arrange
        InboundVoiceEvent routed = null;
        var sink = new Mock<IInboundVoiceEventSink>();
        sink
            .Setup(value => value.RouteAsync(It.IsAny<InboundVoiceEvent>(), It.IsAny<CancellationToken>()))
            .Callback<InboundVoiceEvent, CancellationToken>((inboundEvent, _) => routed = inboundEvent)
            .ReturnsAsync(new InboundVoiceRouteOutcome());

        // Act
        await new ContactCenterTelnyxInboundCallRouter(sink.Object).RouteAsync(
            new TelnyxCallEvent { CallControlId = "v3:inbound", From = CraftedCaller, To = "+17025550100" },
            _now,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(CleanedCaller, routed.FromAddress);
    }

    [Fact]
    public async Task TheTelnyxWebhook_MapsAnInboundCallsEvent_WithItsCallerIdCleaned()
    {
        // Arrange
        ProviderVoiceEvent ingested = null;
        var ingestor = new Mock<INormalizedVoiceEventIngestor>();
        ingestor
            .Setup(value => value.IngestAsync(It.IsAny<ProviderVoiceEvent>(), It.IsAny<CancellationToken>()))
            .Callback<ProviderVoiceEvent, CancellationToken>((providerEvent, _) => ingested = providerEvent)
            .ReturnsAsync(true);
        var orchestrator = new Mock<ITelnyxOutboundBridgeOrchestrator>();
        orchestrator
            .Setup(value => value.AdvanceAsync(It.IsAny<TelnyxCallEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TelnyxOutboundBridgeLeg.None);
        var options = new TestOptionsMonitor<TelnyxOptions>(new TelnyxOptions { ApiBaseUrl = "https://api.telnyx.test/v2/", ApiKey = "KEY" });
        var webhooks = new TelnyxWebhookService(
            ingestor.Object,
            new Mock<ITelnyxInboundCallRouter>(MockBehavior.Strict).Object,
            new Mock<IInboundVoiceDigitsSink>(MockBehavior.Strict).Object,
            orchestrator.Object,
            [],
            [],
            Mock.Of<IExternalTransferOutcomeSink>(),
            new TelnyxApiClient(
                new HttpClient { BaseAddress = new Uri("https://api.telnyx.test/v2/") },
                options,
                new TelnyxApiRetryPolicy(TimeSpan.Zero),
                NullLogger<TelnyxApiClient>.Instance),
            new TelnyxHangUpAfterSpeechRegistry(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()))),
            Clock(),
            NullLogger<TelnyxWebhookService>.Instance);

        // Act
        await webhooks.ProcessAsync(new TelnyxCallEvent
        {
            EventType = "call.initiated",
            EventId = "evt-1",
            CallControlId = "v3:inbound",
            Direction = "incoming",
            From = CraftedCaller,
            To = "+17025550100",
            OccurredUtc = _now,
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(ingested);
        Assert.Equal(CleanedCaller, ingested.FromAddress);
    }

    private static TelephonyCall InboundCall(string from)
        => new()
        {
            CallId = "call-1",
            ProviderName = "provider-1",
            From = from,
            To = "+17025550100",
            Direction = CallDirection.Inbound,
            State = CallState.Ringing,
        };

    private static DefaultIncomingCallDispatcher CreateDispatcher(ITelephonyInteractionStore store)
        => new(
            HubContext(Mock.Of<ITelephonyClient>()).Object,
            [],
            store,
            Clock(),
            new Mock<ILogger<DefaultIncomingCallDispatcher>>().Object,
            new ShellSettings { Name = "TenantA" });

    private static Mock<IHubContext<TelephonyHub, ITelephonyClient>> HubContext(ITelephonyClient client)
    {
        var clients = new Mock<IHubClients<ITelephonyClient>>();
        clients.Setup(value => value.Group(TenantSignalRGroupName.ForUser("TenantA", "user-1"))).Returns(client);

        var hubContext = new Mock<IHubContext<TelephonyHub, ITelephonyClient>>();
        hubContext.SetupGet(value => value.Clients).Returns(clients.Object);

        return hubContext;
    }

    private static IClock Clock()
        => Mock.Of<IClock>(clock => clock.UtcNow == _now);
}

using CrestApps.OrchardCore.SignalR.Core;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Hubs;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// A call that is ringing a user is reported to that user as ringing until they join it, even when the provider's
/// lookup of the call calls it connected.
/// </summary>
/// <remarks>
/// A provider's lookup is of the call, not of the user's part in it. A caller waiting for an agent is on a leg the
/// platform answered itself, and Telnyx can only say that a call is alive, which the provider maps to connected. The
/// active-calls lookup the soft phone polls every few seconds handed that straight to the ringing phone, so a call
/// nobody had answered showed as connected.
/// </remarks>
public sealed class TelephonyAwaitingAnswerTests
{
    private static readonly DateTime _now = new(2026, 9, 24, 18, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(CallState.Connected)]
    [InlineData(CallState.OnHold)]
    [InlineData(CallState.Connecting)]
    public async Task GetActiveCallsAsync_ReportsACallTheUserHasNotJoined_AsRinging(CallState reported)
    {
        // Arrange
        var entry = CreateEntry(awaitingAnswer: true);
        var service = CreateSynchronizationService(entry, reported);

        // Act
        var result = await service.GetActiveCallsAsync("user-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(CallState.Ringing, Assert.Single(result.Calls).State);
    }

    [Fact]
    public async Task GetActiveCallsAsync_ReportsAJoinedCall_AsTheProviderReportsIt()
    {
        // Arrange
        var entry = CreateEntry(awaitingAnswer: false);
        var service = CreateSynchronizationService(entry, CallState.Connected);

        // Act
        var result = await service.GetActiveCallsAsync("user-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(CallState.Connected, Assert.Single(result.Calls).State);
    }

    [Fact]
    public async Task GetActiveCallAsync_ReportsACallTheUserHasNotJoined_AsRinging()
    {
        // Arrange
        var entry = CreateEntry(awaitingAnswer: true);
        var service = CreateSynchronizationService(entry, CallState.Connected);

        // Act
        var result = await service.GetActiveCallAsync("user-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(CallState.Ringing, result.Call.State);
    }

    [Theory]
    [InlineData(VoiceCallState.Connected, false)]
    [InlineData(VoiceCallState.OnHold, false)]
    [InlineData(VoiceCallState.Ended, false)]
    [InlineData(VoiceCallState.Ringing, true)]
    public async Task TheCallHistoryProjection_ClearsTheRingOnceTheUserIsOnTheCall(VoiceCallState state, bool stillAwaiting)
    {
        // Arrange
        var entry = CreateEntry(awaitingAnswer: true);
        var store = new Mock<ITelephonyInteractionStore>();
        store.SetupRetryingUpdates(entry);

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        var handler = new TelephonyCallHistoryVoiceEventHandler(
            store.Object,
            CreateHubContext().Object,
            clock.Object,
            NullLogger<TelephonyCallHistoryVoiceEventHandler>.Instance,
            new ShellSettings { Name = "TenantA" });

        // Act
        await handler.HandleAsync(new ProviderVoiceEvent
        {
            ProviderName = "provider-1",
            ProviderCallId = "call-1",
            State = state,
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(stillAwaiting, entry.AwaitingAnswer);
    }

    [Fact]
    public async Task TheIncomingCallDispatcher_RecordsARingingCallAsAwaitingAnswer()
    {
        // Arrange
        TelephonyInteraction created = null;
        var store = new Mock<ITelephonyInteractionStore>();
        store
            .Setup(value => value.CreateAsync(It.IsAny<TelephonyInteraction>(), It.IsAny<CancellationToken>()))
            .Callback<TelephonyInteraction, CancellationToken>((interaction, _) => created = interaction)
            .Returns(Task.CompletedTask);

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        var dispatcher = new DefaultIncomingCallDispatcher(
            CreateHubContext().Object,
            [],
            store.Object,
            clock.Object,
            new Mock<ILogger<DefaultIncomingCallDispatcher>>().Object,
            new ShellSettings { Name = "TenantA" });

        // Act
        await dispatcher.DispatchAsync("user-1", new TelephonyCall
        {
            CallId = "call-1",
            ProviderName = "provider-1",
            Direction = CallDirection.Inbound,
            State = CallState.Ringing,
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(created);
        Assert.True(created.AwaitingAnswer);
    }

    private static TelephonyInteraction CreateEntry(bool awaitingAnswer)
        => new()
        {
            InteractionId = "interaction-1",
            CallId = "call-1",
            ProviderName = "provider-1",
            UserId = "user-1",
            From = "+15550001000",
            To = "+15550002000",
            Direction = CallDirection.Inbound,
            Outcome = CallOutcome.InProgress,
            StartedUtc = _now.AddSeconds(-10),
            AwaitingAnswer = awaitingAnswer,
        };

    private static TelephonyInteractionSynchronizationService CreateSynchronizationService(
        TelephonyInteraction entry,
        CallState reported)
    {
        var store = new Mock<ITelephonyInteractionStore>();
        store
            .Setup(value => value.GetActiveByUserAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([entry]);
        store
            .Setup(value => value.FindActiveByUserAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        store.SetupRetryingUpdates(entry);

        var provider = new Mock<ITelephonyProvider>();
        provider
            .As<ITelephonyCallStateProvider>()
            .Setup(value => value.GetCallStateAsync("call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new TelephonyCallLookupResult
            {
                Succeeded = true,
                Found = true,
                Call = new TelephonyCall
                {
                    CallId = "call-1",
                    ProviderName = "provider-1",
                    State = reported,
                },
            });

        var resolver = new Mock<ITelephonyProviderResolver>();
        resolver.Setup(value => value.GetAsync("provider-1")).ReturnsAsync(provider.Object);

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        return new TelephonyInteractionSynchronizationService(
            store.Object,
            resolver.Object,
            CreateHubContext().Object,
            new Mock<IDistributedLock>().Object,
            clock.Object,
            NullLogger<TelephonyInteractionSynchronizationService>.Instance,
            new ShellSettings { Name = "TenantA" },
            Options.Create(new TelephonyCoordinationOptions()));
    }

    private static Mock<IHubContext<TelephonyHub, ITelephonyClient>> CreateHubContext()
    {
        var hubContext = new Mock<IHubContext<TelephonyHub, ITelephonyClient>>();
        var clients = new Mock<IHubClients<ITelephonyClient>>();
        hubContext.SetupGet(value => value.Clients).Returns(clients.Object);
        clients
            .Setup(value => value.Group(TenantSignalRGroupName.ForUser("TenantA", "user-1")))
            .Returns(new Mock<ITelephonyClient>().Object);

        return hubContext;
    }
}

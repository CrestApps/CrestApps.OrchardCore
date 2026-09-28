using CrestApps.OrchardCore.SignalR.Core;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.BackgroundTasks;
using CrestApps.OrchardCore.Telephony.Hubs;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class TelephonyInteractionSynchronizationServiceTests
{
    private static readonly DateTime _now = new(2026, 7, 11, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetActiveCallAsync_WhenProviderNoLongerHasCall_DeletesOrphanedInteraction()
    {
        // Arrange
        var interaction = CreateInteraction();
        var store = new Mock<ITelephonyInteractionStore>();
        store
            .Setup(value => value.FindActiveByUserAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);
        store
            .Setup(value => value.DeleteAsync(interaction, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var (hubContext, client) = CreateHubContext();
        client
            .Setup(value => value.CallStateChanged(It.IsAny<TelephonyCall>()))
            .Returns(Task.CompletedTask);
        var service = CreateService(
            store,
            hubContext,
            new TelephonyCallLookupResult
            {
                Succeeded = true,
                Found = false,
            });

        // Act
        var result = await service.GetActiveCallAsync("user-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.False(result.Found);
        store.Verify(value => value.DeleteAsync(interaction, It.IsAny<CancellationToken>()), Times.Once);
        client.Verify(
            value => value.CallStateChanged(It.Is<TelephonyCall>(call =>
                call.CallId == "call-1" &&
                call.State == CallState.Disconnected)),
            Times.Once);
    }

    [Fact]
    public async Task GetActiveCallAsync_WhenProviderReturnsActiveCall_DoesNotPublishDuplicateStateEvent()
    {
        // Arrange
        var interaction = CreateInteraction();
        var store = new Mock<ITelephonyInteractionStore>();
        store
            .Setup(value => value.FindActiveByUserAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);
        var (hubContext, client) = CreateHubContext();
        var service = CreateService(
            store,
            hubContext,
            new TelephonyCallLookupResult
            {
                Succeeded = true,
                Found = true,
                Call = new TelephonyCall
                {
                    CallId = "call-1",
                    State = CallState.Connected,
                    ProviderName = "provider-1",
                },
            });

        // Act
        var result = await service.GetActiveCallAsync("user-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.True(result.Found);
        client.Verify(value => value.CallStateChanged(It.IsAny<TelephonyCall>()), Times.Never);
    }

    [Fact]
    public async Task GetActiveCallAsync_WhenProviderLookupFails_PreservesInteraction()
    {
        // Arrange
        var interaction = CreateInteraction();
        var store = new Mock<ITelephonyInteractionStore>();
        store
            .Setup(value => value.FindActiveByUserAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);
        var (hubContext, client) = CreateHubContext();
        var service = CreateService(
            store,
            hubContext,
            new TelephonyCallLookupResult
            {
                Succeeded = false,
                Found = false,
                Error = "Provider unavailable.",
            });

        // Act
        var result = await service.GetActiveCallAsync("user-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("Provider unavailable.", result.Error);
        store.Verify(value => value.DeleteAsync(It.IsAny<TelephonyInteraction>(), It.IsAny<CancellationToken>()), Times.Never);
        client.Verify(value => value.CallStateChanged(It.IsAny<TelephonyCall>()), Times.Never);
    }

    [Fact]
    public async Task GetActiveCallAsync_WhenProviderIsNoLongerRegistered_DeletesOrphanedInteraction()
    {
        // Arrange
        var interaction = CreateInteraction();
        var store = new Mock<ITelephonyInteractionStore>();
        store
            .Setup(value => value.FindActiveByUserAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);
        store
            .Setup(value => value.DeleteAsync(interaction, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var (hubContext, client) = CreateHubContext();
        client
            .Setup(value => value.CallStateChanged(It.IsAny<TelephonyCall>()))
            .Returns(Task.CompletedTask);
        var service = CreateService(
            store,
            hubContext,
            new TelephonyCallLookupResult(),
            providerRegistered: false);

        // Act
        var result = await service.GetActiveCallAsync("user-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.False(result.Found);
        store.Verify(value => value.DeleteAsync(interaction, It.IsAny<CancellationToken>()), Times.Once);
        client.Verify(
            value => value.CallStateChanged(It.Is<TelephonyCall>(call =>
                call.CallId == "call-1" &&
                call.State == CallState.Disconnected)),
            Times.Once);
    }

    [Fact]
    public async Task GetActiveCallAsync_WhenNewCallIsStillPropagating_PreservesInteraction()
    {
        // Arrange
        var interaction = CreateInteraction();
        interaction.StartedUtc = _now.AddSeconds(-5);
        var store = new Mock<ITelephonyInteractionStore>();
        store
            .Setup(value => value.FindActiveByUserAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);
        var (hubContext, client) = CreateHubContext();
        var service = CreateService(
            store,
            hubContext,
            new TelephonyCallLookupResult
            {
                Succeeded = true,
                Found = false,
            });

        // Act
        var result = await service.GetActiveCallAsync("user-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.False(result.Found);
        store.Verify(value => value.DeleteAsync(It.IsAny<TelephonyInteraction>(), It.IsAny<CancellationToken>()), Times.Never);
        client.Verify(value => value.CallStateChanged(It.IsAny<TelephonyCall>()), Times.Never);
    }

    [Fact]
    public async Task GetActiveCallsAsync_ReturnsEveryProviderAuthoritativeCall()
    {
        // Arrange
        var firstInteraction = CreateInteraction();
        var secondInteraction = CreateInteraction("call-2");
        var store = new Mock<ITelephonyInteractionStore>();
        store
            .Setup(value => value.GetActiveByUserAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([firstInteraction, secondInteraction]);
        var (hubContext, _) = CreateHubContext();
        var service = CreateService(
            store,
            hubContext,
            new TelephonyCallLookupResult
            {
                Succeeded = true,
                Found = true,
                Call = new TelephonyCall
                {
                    CallId = "call-1",
                    State = CallState.OnHold,
                    ProviderName = "provider-1",
                },
            },
            secondLookup: new TelephonyCallLookupResult
            {
                Succeeded = true,
                Found = true,
                Call = new TelephonyCall
                {
                    CallId = "call-2",
                    To = string.Empty,
                    State = CallState.Connected,
                    ProviderName = "provider-1",
                },
            });

        // Act
        var result = await service.GetActiveCallsAsync("user-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Collection(
            result.Calls,
            call =>
            {
                Assert.Equal("call-1", call.CallId);
                Assert.Equal("+15550002000", call.To);
            },
            call =>
            {
                Assert.Equal("call-2", call.CallId);
                Assert.Equal("+15550002000", call.To);
            });
    }

    [Fact]
    public async Task ReconcileProviderInteractionsAsync_WhenProviderStateChanged_PersistsThroughTheRetryingUpdate()
    {
        // Arrange
        var interaction = CreateInteraction();
        var store = new Mock<ITelephonyInteractionStore>();
        store
            .Setup(value => value.GetActiveAsync("provider-1", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([interaction]);
        store.SetupRetryingUpdates(interaction);
        var (hubContext, client) = CreateHubContext();
        client
            .Setup(value => value.CallStateChanged(It.IsAny<TelephonyCall>()))
            .Returns(Task.CompletedTask);
        var service = CreateService(
            store,
            hubContext,
            new TelephonyCallLookupResult
            {
                Succeeded = true,
                Found = true,
                Call = new TelephonyCall
                {
                    CallId = "call-1",
                    ProviderName = "provider-1",
                    From = "+15559990000",
                    State = CallState.Connected,
                },
            },
            lockAcquired: true);

        // Act
        var changed = await service.ReconcileProviderInteractionsAsync(
            "provider-1",
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, changed);
        Assert.Equal("+15559990000", interaction.From);

        store.Verify(
            value => value.UpdateByIdAsync(
                "interaction-1",
                It.IsAny<Func<TelephonyInteraction, bool>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        store.Verify(
            value => value.UpdateAsync(It.IsAny<TelephonyInteraction>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ReconcileProviderInteractionsAsync_WhenCallIsMissing_NotifiesUserAndDeletesOrphan()
    {
        // Arrange
        var interaction = CreateInteraction();
        var store = new Mock<ITelephonyInteractionStore>();
        store
            .Setup(value => value.GetActiveAsync("provider-1", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([interaction]);
        store
            .Setup(value => value.DeleteAsync(interaction, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var (hubContext, client) = CreateHubContext();
        client
            .Setup(value => value.CallStateChanged(It.IsAny<TelephonyCall>()))
            .Returns(Task.CompletedTask);
        var service = CreateService(
            store,
            hubContext,
            new TelephonyCallLookupResult
            {
                Succeeded = true,
                Found = false,
            },
            lockAcquired: true);

        // Act
        var changed = await service.ReconcileProviderInteractionsAsync(
            "provider-1",
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, changed);
        client.Verify(
            value => value.CallStateChanged(It.Is<TelephonyCall>(call =>
                call.CallId == "call-1" &&
                call.State == CallState.Disconnected)),
            Times.Once);
        store.Verify(value => value.DeleteAsync(interaction, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReconciliationBackgroundTask_FreshScope_PerformsTelephonyReconciliation()
    {
        // Arrange
        var synchronizationService = new Mock<ITelephonyInteractionSynchronizationService>();
        synchronizationService
            .Setup(value => value.ReconcileActiveInteractionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        var services = new ServiceCollection()
            .AddSingleton(synchronizationService.Object)
            .AddSingleton<ILogger<TelephonyInteractionReconciliationBackgroundTask>>(
                NullLogger<TelephonyInteractionReconciliationBackgroundTask>.Instance)
            .BuildServiceProvider();
        var task = new TelephonyInteractionReconciliationBackgroundTask();

        // Act
        await task.DoWorkAsync(services, TestContext.Current.CancellationToken);

        // Assert
        synchronizationService.Verify(
            value => value.ReconcileActiveInteractionsAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // A browser-originated call is recorded by the client with a call id but no provider identity, because the
    // provider SDK placed it directly and the platform never saw it. The reconciler has nothing to look it up
    // against; treating that as "orphaned" announced a terminal state to a soft phone still on the call, which
    // then hung up its own live session -- on the first sweep after every keypad call passed the minute mark.
    [Fact]
    public async Task ReconcileActiveInteractionsAsync_WhenInteractionIsClientRecordedAndRecent_LeavesItAloneWithoutNotifying()
    {
        // Arrange
        var interaction = CreateInteraction("browser-1789315548135");
        interaction.ProviderName = null;
        var store = new Mock<ITelephonyInteractionStore>();
        store
            .Setup(value => value.GetActiveAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([interaction]);
        var (hubContext, client) = CreateHubContext();
        var service = CreateService(store, hubContext, new TelephonyCallLookupResult(), lockAcquired: true);

        // Act
        var changed = await service.ReconcileActiveInteractionsAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, changed);
        store.Verify(value => value.DeleteAsync(It.IsAny<TelephonyInteraction>(), It.IsAny<CancellationToken>()), Times.Never);
        client.Verify(value => value.CallStateChanged(It.IsAny<TelephonyCall>()), Times.Never);
    }

    // The one case the sweep still owns for a client-recorded call: the browser went away mid-call and never
    // reported the end. That is caught by age and removed quietly -- there is no live phone left to tell, and a
    // late announcement could only reach a phone that has since started another call.
    [Fact]
    public async Task ReconcileActiveInteractionsAsync_WhenInteractionIsClientRecordedAndStale_RemovesItWithoutNotifying()
    {
        // Arrange
        var interaction = CreateInteraction("browser-1789315548135");
        interaction.ProviderName = null;
        interaction.StartedUtc = _now.AddHours(-5);
        var store = new Mock<ITelephonyInteractionStore>();
        store
            .Setup(value => value.GetActiveAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([interaction]);
        store
            .Setup(value => value.DeleteAsync(interaction, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var (hubContext, client) = CreateHubContext();
        var service = CreateService(store, hubContext, new TelephonyCallLookupResult(), lockAcquired: true);

        // Act
        var changed = await service.ReconcileActiveInteractionsAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, changed);
        store.Verify(value => value.DeleteAsync(interaction, It.IsAny<CancellationToken>()), Times.Once);
        client.Verify(value => value.CallStateChanged(It.IsAny<TelephonyCall>()), Times.Never);
    }

    // Bug: the agent held a keypad call, dialed a second number, and both calls dropped. The phone never reported the
    // first call's end, so its history stayed "in progress" with no end time -- for good, short of a four-hour ceiling
    // that would then have deleted it. A call the phone stopped reporting is settled, at the last moment the phone was
    // heard from, without telling a phone that is no longer there.
    [Fact]
    public async Task ReconcileActiveInteractionsAsync_WhenAClientRecordedCallStoppedBeingReported_SettlesItAtTheLastReport()
    {
        // Arrange
        var interaction = CreateInteraction("browser-1790295290897");
        interaction.ProviderName = null;
        interaction.StartedUtc = _now.AddMinutes(-20);
        interaction.Put(new ClientRecordedCallActivity
        {
            LastReportedUtc = _now.AddMinutes(-18),
            ConnectedUtc = _now.AddMinutes(-19.5),
        });
        var store = new Mock<ITelephonyInteractionStore>();
        store
            .Setup(value => value.GetActiveAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([interaction]);
        store.SetupRetryingUpdates(interaction);
        var (hubContext, client) = CreateHubContext();
        var service = CreateService(store, hubContext, new TelephonyCallLookupResult(), lockAcquired: true);

        // Act
        var changed = await service.ReconcileActiveInteractionsAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, changed);
        Assert.Equal(CallOutcome.Completed, interaction.Outcome);
        Assert.Equal(_now.AddMinutes(-18), interaction.EndedUtc);
        Assert.Equal(120, interaction.DurationSeconds);
        Assert.True(ActivityOf(interaction).EndedUnreported);
        store.Verify(value => value.DeleteAsync(It.IsAny<TelephonyInteraction>(), It.IsAny<CancellationToken>()), Times.Never);
        client.Verify(value => value.CallStateChanged(It.IsAny<TelephonyCall>()), Times.Never);
    }

    // A call the phone never reported as anything but placed -- the record a phone older than the reports leaves --
    // is settled as it started: never connected, no talk time.
    [Fact]
    public async Task ReconcileActiveInteractionsAsync_WhenAClientRecordedCallWasNeverReported_SettlesItAsNeverConnected()
    {
        // Arrange
        var interaction = CreateInteraction("browser-1790295317044");
        interaction.ProviderName = null;
        interaction.StartedUtc = _now.AddMinutes(-10);
        var store = new Mock<ITelephonyInteractionStore>();
        store
            .Setup(value => value.GetActiveAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([interaction]);
        store.SetupRetryingUpdates(interaction);
        var (hubContext, _) = CreateHubContext();
        var service = CreateService(store, hubContext, new TelephonyCallLookupResult(), lockAcquired: true);

        // Act
        var changed = await service.ReconcileActiveInteractionsAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, changed);
        Assert.Equal(CallOutcome.Canceled, interaction.Outcome);
        Assert.Equal(interaction.StartedUtc, interaction.EndedUtc);
        Assert.Equal(0, interaction.DurationSeconds);
    }

    // A call the phone still reports is up, however long ago it started.
    [Fact]
    public async Task ReconcileActiveInteractionsAsync_WhenAClientRecordedCallIsStillReported_LeavesItInProgress()
    {
        // Arrange
        var interaction = CreateInteraction("browser-1790295290897");
        interaction.ProviderName = null;
        interaction.StartedUtc = _now.AddMinutes(-40);
        interaction.Put(new ClientRecordedCallActivity { LastReportedUtc = _now.AddSeconds(-30), ConnectedUtc = _now.AddMinutes(-39) });
        var store = new Mock<ITelephonyInteractionStore>();
        store
            .Setup(value => value.GetActiveAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([interaction]);
        store.SetupRetryingUpdates(interaction);
        var (hubContext, _) = CreateHubContext();
        var service = CreateService(store, hubContext, new TelephonyCallLookupResult(), lockAcquired: true);

        // Act
        var changed = await service.ReconcileActiveInteractionsAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, changed);
        Assert.Equal(CallOutcome.InProgress, interaction.Outcome);
        Assert.Null(interaction.EndedUtc);
    }

    // Bug: a Contact Center offer rings the agent's phone while the caller's own leg is live (answered by the platform
    // to play hold music). The provider can only say that leg is alive, and the sweep told the ringing phone the call
    // was "Connected": the incoming-call prompt vanished and the phone showed a call in progress that nobody had
    // accepted. A sweep only knows whether a call still exists; how a live call stands is the real-time events' to say.
    [Theory]
    [InlineData(CallState.Connected)]
    [InlineData(CallState.OnHold)]
    [InlineData(CallState.Ringing)]
    public async Task ReconcileActiveInteractionsAsync_WhenTheCallIsStillLive_DoesNotAnnounceAState(CallState state)
    {
        // Arrange
        var interaction = CreateInteraction();
        var store = new Mock<ITelephonyInteractionStore>();
        store
            .Setup(value => value.GetActiveAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([interaction]);
        store.SetupRetryingUpdates(interaction);
        var (hubContext, client) = CreateHubContext();
        client
            .Setup(value => value.CallStateChanged(It.IsAny<TelephonyCall>()))
            .Returns(Task.CompletedTask);
        var service = CreateService(
            store,
            hubContext,
            new TelephonyCallLookupResult
            {
                Succeeded = true,
                Found = true,
                Call = new TelephonyCall
                {
                    CallId = "call-1",
                    ProviderName = "provider-1",
                    State = state,
                },
            },
            lockAcquired: true);

        // Act
        await service.ReconcileActiveInteractionsAsync(TestContext.Current.CancellationToken);

        // Assert
        client.Verify(value => value.CallStateChanged(It.IsAny<TelephonyCall>()), Times.Never);
    }

    // The end of a call is what the sweep is for: a provider that still knows the call but reports it over is announced.
    [Fact]
    public async Task ReconcileActiveInteractionsAsync_WhenTheProviderReportsTheCallOver_AnnouncesIt()
    {
        // Arrange
        var interaction = CreateInteraction();
        var store = new Mock<ITelephonyInteractionStore>();
        store
            .Setup(value => value.GetActiveAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([interaction]);
        store.SetupRetryingUpdates(interaction);
        var (hubContext, client) = CreateHubContext();
        client
            .Setup(value => value.CallStateChanged(It.IsAny<TelephonyCall>()))
            .Returns(Task.CompletedTask);
        var service = CreateService(
            store,
            hubContext,
            new TelephonyCallLookupResult
            {
                Succeeded = true,
                Found = true,
                Call = new TelephonyCall
                {
                    CallId = "call-1",
                    ProviderName = "provider-1",
                    State = CallState.Disconnected,
                },
            },
            lockAcquired: true);

        // Act
        await service.ReconcileActiveInteractionsAsync(TestContext.Current.CancellationToken);

        // Assert
        client.Verify(
            value => value.CallStateChanged(It.Is<TelephonyCall>(call =>
                call.CallId == "call-1" && call.State == CallState.Disconnected)),
            Times.Once);
    }

    private static TelephonyInteractionSynchronizationService CreateService(
        Mock<ITelephonyInteractionStore> store,
        Mock<IHubContext<TelephonyHub, ITelephonyClient>> hubContext,
        TelephonyCallLookupResult lookup,
        bool lockAcquired = false,
        bool providerRegistered = true,
        TelephonyCallLookupResult secondLookup = null)
    {
        var provider = new Mock<ITelephonyProvider>();
        provider
            .As<ITelephonyCallStateProvider>()
            .Setup(value => value.GetCallStateAsync("call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(lookup);

        if (secondLookup is not null)
        {
            provider
                .As<ITelephonyCallStateProvider>()
                .Setup(value => value.GetCallStateAsync("call-2", It.IsAny<CancellationToken>()))
                .ReturnsAsync(secondLookup);
        }
        var resolver = new Mock<ITelephonyProviderResolver>();

        if (providerRegistered)
        {
            resolver.Setup(value => value.GetAsync("provider-1")).ReturnsAsync(provider.Object);
        }
        else
        {
            resolver.Setup(value => value.GetAsync("provider-1")).ReturnsAsync((ITelephonyProvider)null);
        }

        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(value => value.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync((null, lockAcquired));
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        return new TelephonyInteractionSynchronizationService(
            store.Object,
            resolver.Object,
            hubContext.Object,
            distributedLock.Object,
            clock.Object,
            NullLogger<TelephonyInteractionSynchronizationService>.Instance,
            new ShellSettings
            {
                Name = "TenantA",
            },
            Options.Create(new TelephonyCoordinationOptions()));
    }

    private static (Mock<IHubContext<TelephonyHub, ITelephonyClient>> HubContext, Mock<ITelephonyClient> Client) CreateHubContext()
    {
        var hubContext = new Mock<IHubContext<TelephonyHub, ITelephonyClient>>();
        var clients = new Mock<IHubClients<ITelephonyClient>>();
        var client = new Mock<ITelephonyClient>();
        hubContext.SetupGet(value => value.Clients).Returns(clients.Object);
        clients.Setup(value => value.Group(TenantSignalRGroupName.ForUser("TenantA", "user-1"))).Returns(client.Object);

        return (hubContext, client);
    }

    private static ClientRecordedCallActivity ActivityOf(TelephonyInteraction interaction)
        => interaction.TryGet<ClientRecordedCallActivity>(out var activity) ? activity : null;

    private static TelephonyInteraction CreateInteraction(string callId = "call-1")
    {
        return new TelephonyInteraction
        {
            InteractionId = "interaction-1",
            CallId = callId,
            ProviderName = "provider-1",
            UserId = "user-1",
            From = "+15550001000",
            To = "+15550002000",
            Direction = CallDirection.Outbound,
            Outcome = CallOutcome.InProgress,
            StartedUtc = _now.AddMinutes(-3),
        };
    }
}

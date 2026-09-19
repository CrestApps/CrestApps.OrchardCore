using CrestApps.Core.Locking;
using CrestApps.Core.SignalR;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.BackgroundTasks;
using CrestApps.OrchardCore.Telephony.Hubs;
using CrestApps.Core.Telephony.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;
using CrestApps.Core.Telephony;
using CrestApps.Core.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;

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
            .AddSingleton<ILogger<TelephonyInteractionReconciliationCycle>>(
                NullLogger<TelephonyInteractionReconciliationCycle>.Instance)
            .AddScoped<ITelephonyInteractionReconciliationCycle, TelephonyInteractionReconciliationCycle>()
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

        var distributedLock = new Mock<IDistributedLockProvider>();
        distributedLock
            .Setup(value => value.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((null, lockAcquired));
        var clock = new FakeTimeProvider();
        clock.SetUtcNow(_now);

        return new TelephonyInteractionSynchronizationService(
            store.Object,
            resolver.Object,
            hubContext.Object,
            distributedLock.Object,
            clock,
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

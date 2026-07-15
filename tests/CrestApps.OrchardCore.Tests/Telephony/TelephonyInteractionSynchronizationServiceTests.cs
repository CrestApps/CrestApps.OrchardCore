using CrestApps.OrchardCore.SignalR;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.BackgroundTasks;
using CrestApps.OrchardCore.Telephony.Hubs;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
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
            .Setup(value => value.ListActiveByUserAsync("user-1", It.IsAny<CancellationToken>()))
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
    public async Task ReconcileProviderInteractionsAsync_WhenCallIsMissing_NotifiesUserAndDeletesOrphan()
    {
        // Arrange
        var interaction = CreateInteraction();
        var store = new Mock<ITelephonyInteractionStore>();
        store
            .Setup(value => value.ListActiveAsync("provider-1", It.IsAny<CancellationToken>()))
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
            });
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

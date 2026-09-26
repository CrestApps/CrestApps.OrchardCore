using CrestApps.OrchardCore.Asterisk.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class AsteriskPendingCallerTerminationReconcilerTests
{
    [Fact]
    public async Task ReconcileAsync_WhenTheClaimIsAffirmedAndHangupSucceeds_ReleasesTheClaimAndResolvesTheChannel()
    {
        // Arrange
        const string channelId = "reconcile-success-channel";
        var registry = CreateRegistry("ReconcileTenantSuccess");
        registry.Enqueue(channelId);

        var ariClient = new Mock<IAsteriskAriClient>();
        ariClient
            .Setup(client => client.HangupAsync(channelId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var bindingStore = new Mock<IAsteriskChannelTenantBindingStore>();
        bindingStore
            .Setup(store => store.TryClaimChannelForTerminationAsync(channelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var reconciler = CreateReconciler(registry, ariClient.Object, bindingStore.Object);

        // Act
        await reconciler.ReconcileAsync("Asterisk", TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(registry.GetPending());
        ariClient.Verify(client => client.HangupAsync(channelId, It.IsAny<CancellationToken>()), Times.Once);
        bindingStore.Verify(store => store.ReleaseTerminationClaim(channelId), Times.Once);
    }

    [Fact]
    public async Task ReconcileAsync_WhenAConcurrentCreateRecoveredTheCaller_DropsTheChannelWithoutHangingUp()
    {
        // Arrange
        const string channelId = "reconcile-recovered-channel";
        var registry = CreateRegistry("ReconcileTenantRecovered");
        registry.Enqueue(channelId);

        var ariClient = new Mock<IAsteriskAriClient>();

        var bindingStore = new Mock<IAsteriskChannelTenantBindingStore>();
        bindingStore
            .Setup(store => store.TryClaimChannelForTerminationAsync(channelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var reconciler = CreateReconciler(registry, ariClient.Object, bindingStore.Object);

        // Act
        await reconciler.ReconcileAsync("Asterisk", TestContext.Current.CancellationToken);

        // Assert
        // A binding now exists, so the caller was legitimately recovered into a live call: it is dropped from the
        // pending set and never hung up, and no claim is released because none was affirmed.
        Assert.Empty(registry.GetPending());
        ariClient.Verify(client => client.HangupAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        bindingStore.Verify(store => store.ReleaseTerminationClaim(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ReconcileAsync_WhenTheCreateLockIsWedged_LeavesTheChannelPendingForTheNextSweep()
    {
        // Arrange
        const string channelId = "reconcile-wedged-channel";
        var registry = CreateRegistry("ReconcileTenantWedged");
        registry.Enqueue(channelId);

        var ariClient = new Mock<IAsteriskAriClient>();

        var bindingStore = new Mock<IAsteriskChannelTenantBindingStore>();
        bindingStore
            .Setup(store => store.TryClaimChannelForTerminationAsync(channelId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AsteriskChannelBindingCreateTimeoutException(channelId, TimeSpan.FromSeconds(10)));

        var reconciler = CreateReconciler(registry, ariClient.Object, bindingStore.Object);

        // Act
        await reconciler.ReconcileAsync("Asterisk", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(channelId, registry.GetPending());
        ariClient.Verify(client => client.HangupAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        bindingStore.Verify(store => store.ReleaseTerminationClaim(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ReconcileAsync_WhenHangupThrows_KeepsTheChannelAndClaimForTheNextSweep()
    {
        // Arrange
        const string channelId = "reconcile-throws-channel";
        var registry = CreateRegistry("ReconcileTenantThrows");
        registry.Enqueue(channelId);

        var ariClient = new Mock<IAsteriskAriClient>();
        ariClient
            .Setup(client => client.HangupAsync(channelId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("transient ARI failure"));

        var bindingStore = new Mock<IAsteriskChannelTenantBindingStore>();
        bindingStore
            .Setup(store => store.TryClaimChannelForTerminationAsync(channelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var reconciler = CreateReconciler(registry, ariClient.Object, bindingStore.Object);

        // Act
        await reconciler.ReconcileAsync("Asterisk", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(channelId, registry.GetPending());
        bindingStore.Verify(store => store.ReleaseTerminationClaim(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ReconcileAsync_WhenHangupFailsThenSucceedsOnALaterSweep_EventuallyResolvesTheChannel()
    {
        // Arrange
        const string channelId = "reconcile-eventual-channel";
        var registry = CreateRegistry("ReconcileTenantEventual");
        registry.Enqueue(channelId);

        var attempts = 0;
        var ariClient = new Mock<IAsteriskAriClient>();
        ariClient
            .Setup(client => client.HangupAsync(channelId, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                attempts++;

                return attempts == 1
                    ? Task.FromException(new InvalidOperationException("transient ARI failure"))
                    : Task.CompletedTask;
            });

        var bindingStore = new Mock<IAsteriskChannelTenantBindingStore>();
        bindingStore
            .Setup(store => store.TryClaimChannelForTerminationAsync(channelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var reconciler = CreateReconciler(registry, ariClient.Object, bindingStore.Object);

        // Act
        await reconciler.ReconcileAsync("Asterisk", TestContext.Current.CancellationToken);
        var pendingAfterFirstSweep = registry.GetPending();

        await reconciler.ReconcileAsync("Asterisk", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(channelId, pendingAfterFirstSweep);
        Assert.Empty(registry.GetPending());
        bindingStore.Verify(store => store.ReleaseTerminationClaim(channelId), Times.Once);
    }

    private static AsteriskPendingCallerTerminationRegistry CreateRegistry(string tenantName)
    {
        return new AsteriskPendingCallerTerminationRegistry(new ShellSettings { Name = tenantName });
    }

    private static AsteriskPendingCallerTerminationReconciler CreateReconciler(
        IAsteriskPendingCallerTerminationRegistry registry,
        IAsteriskAriClient ariClient,
        IAsteriskChannelTenantBindingStore bindingStore)
    {
        return new AsteriskPendingCallerTerminationReconciler(
            registry,
            bindingStore,
            ariClient,
            NullLogger<AsteriskPendingCallerTerminationReconciler>.Instance);
    }
}

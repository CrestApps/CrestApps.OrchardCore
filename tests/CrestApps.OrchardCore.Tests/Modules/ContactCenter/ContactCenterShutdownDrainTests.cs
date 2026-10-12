using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// A node that is shutting down kept offering, assigning and dialing until the process went, so it could start
/// routing work in its last seconds that it was not alive to finish. The drain closes routing the moment the host
/// begins to stop, and leaves the voice path open so calls already up are still followed.
/// </summary>
public sealed class ContactCenterShutdownDrainTests
{
    [Fact]
    public async Task Stopping_RefusesNewRoutingWork_ButKeepsTheVoicePathOpen()
    {
        // Arrange
        var lifetime = new StubHostApplicationLifetime();
        var manager = CreateAdmissibleWorkManager();
        using var drain = CreateDrain(lifetime, manager);
        await drain.ActivatedAsync();

        // Act
        lifetime.StopApplication();

        // Assert
        Assert.Null(manager.TryEnter(ContactCenterConstants.Feature.Queues));
        Assert.Null(manager.TryEnter(ContactCenterConstants.Feature.Dialer));
        Assert.Null(manager.TryEnter(ContactCenterConstants.Feature.DialerPaced));

        using var voiceLease = manager.TryEnter(ContactCenterConstants.Feature.Voice);
        Assert.NotNull(voiceLease);
    }

    [Fact]
    public async Task Stopping_WaitsForRoutingWorkAlreadyAdmitted()
    {
        // Arrange
        var lifetime = new StubHostApplicationLifetime();
        var manager = CreateAdmissibleWorkManager();
        using var drain = CreateDrain(lifetime, manager, shutdownDrainTimeoutSeconds: 30);
        await drain.ActivatedAsync();
        var inFlight = manager.TryEnter(ContactCenterConstants.Feature.Queues);

        // Act
        // The host runs its stopping callbacks on the thread that stops it, so the stop is made off the test thread.
        var stopping = Task.Run(lifetime.StopApplication, TestContext.Current.CancellationToken);
        var finishedEarly = await Task.WhenAny(stopping, Task.Delay(200, TestContext.Current.CancellationToken)) == stopping;
        inFlight.Dispose();

        // Assert
        Assert.False(finishedEarly);
        await stopping.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Stopping_GivesUpWaiting_OnceTheTimeoutPasses()
    {
        // Arrange
        var lifetime = new StubHostApplicationLifetime();
        var manager = CreateAdmissibleWorkManager();
        using var drain = CreateDrain(lifetime, manager, shutdownDrainTimeoutSeconds: 1);
        await drain.ActivatedAsync();
        using var stuck = manager.TryEnter(ContactCenterConstants.Feature.Queues);

        // Act
        var stopping = Task.Run(lifetime.StopApplication, TestContext.Current.CancellationToken);

        // Assert
        await stopping.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Stopping_DoesNotWait_WhenTheTimeoutIsZero()
    {
        // Arrange
        var lifetime = new StubHostApplicationLifetime();
        var manager = CreateAdmissibleWorkManager();
        using var drain = CreateDrain(lifetime, manager, shutdownDrainTimeoutSeconds: 0);
        await drain.ActivatedAsync();
        using var inFlight = manager.TryEnter(ContactCenterConstants.Feature.Queues);

        // Act
        lifetime.StopApplication();

        // Assert
        Assert.Null(manager.TryEnter(ContactCenterConstants.Feature.Queues));
    }

    [Fact]
    public async Task Stopping_DoesNothing_AfterTheShellIsDisposed()
    {
        // Arrange
        // A reloaded tenant disposes the old shell; its registration must not quiesce work after that.
        var lifetime = new StubHostApplicationLifetime();
        var manager = CreateAdmissibleWorkManager();
        var drain = CreateDrain(lifetime, manager);
        await drain.ActivatedAsync();
        drain.Dispose();

        // Act
        lifetime.StopApplication();

        // Assert
        using var lease = manager.TryEnter(ContactCenterConstants.Feature.Queues);
        Assert.NotNull(lease);
    }

    [Fact]
    public async Task ActivatedAsync_QuiescesAtOnce_WhenTheHostIsAlreadyStopping()
    {
        // Arrange
        var lifetime = new StubHostApplicationLifetime();
        lifetime.StopApplication();
        var manager = CreateAdmissibleWorkManager();
        using var drain = CreateDrain(lifetime, manager);

        // Act
        await drain.ActivatedAsync();

        // Assert
        Assert.Null(manager.TryEnter(ContactCenterConstants.Feature.Queues));
    }

    private static ContactCenterShutdownDrain CreateDrain(
        IHostApplicationLifetime lifetime,
        IContactCenterFeatureWorkManager manager,
        int shutdownDrainTimeoutSeconds = ContactCenterFeatureLifecycleOptions.DefaultShutdownDrainTimeoutSeconds)
        => new(
            lifetime,
            manager,
            Options.Create(new ContactCenterFeatureLifecycleOptions { ShutdownDrainTimeoutSeconds = shutdownDrainTimeoutSeconds }),
            NullLogger<ContactCenterShutdownDrain>.Instance);

    private static ContactCenterFeatureWorkManager CreateAdmissibleWorkManager()
    {
        var state = new ContactCenterTopologyState();
        state.Record(new ContactCenterTopologyValidationResult
        {
            DeclaredProfileId = ContactCenterTopologyProfiles.SingleNodeDistributedId,
            IsProductionTopology = true,
        });

        return new ContactCenterFeatureWorkManager(state);
    }

    private sealed class StubHostApplicationLifetime : IHostApplicationLifetime
    {
        private readonly CancellationTokenSource _stopping = new();

        public CancellationToken ApplicationStarted => CancellationToken.None;

        public CancellationToken ApplicationStopping => _stopping.Token;

        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication() => _stopping.Cancel();
    }
}

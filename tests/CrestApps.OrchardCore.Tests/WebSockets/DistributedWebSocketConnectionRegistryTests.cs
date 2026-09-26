using CrestApps.OrchardCore.Tests.WebSockets.Doubles;
using CrestApps.OrchardCore.WebSockets.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.WebSockets;

/// <summary>
/// A provider dials back to a hosted WebSocket, and the callback lands on whichever node the load balancer
/// chooses. The per-node registry answers "no rendezvous pending" for a key another node is holding, which is the
/// same answer it gives for a key that never existed or has expired — so a multi-node deployment loses call audio
/// and the log says nothing about why.
/// <para>
/// A live socket cannot be moved between processes, so this registry buys discovery and affinity, not transparent
/// hand-off: the wrong node still cannot serve the socket, but it now knows that is what happened and says so.
/// </para>
/// </summary>
public sealed class DistributedWebSocketConnectionRegistryTests
{
    [Fact]
    public async Task ARendezvous_RegisteredAndClaimedOnTheSameNode_IsHandedOver()
    {
        // Arrange
        var owners = new FakeRendezvousOwnerStore();
        var registry = CreateRegistry(owners, "node-a");
        var rendezvous = await registry.RegisterAsync("key-1", TestContext.Current.CancellationToken);

        // Act
        var claimed = await registry.TryClaimAsync("key-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(rendezvous, claimed);
    }

    [Fact]
    public async Task Registering_RecordsTheOwningNode_SoAnotherNodeCanTellWhoHasIt()
    {
        // Arrange
        var owners = new FakeRendezvousOwnerStore();
        var registry = CreateRegistry(owners, "node-a");

        // Act
        await registry.RegisterAsync("key-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("node-a", await owners.GetOwnerAsync("key-1", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Registering_ExpiresTheOwnership_SoAnAbandonedKeyDoesNotHoldTheNameForever()
    {
        // Arrange
        // The starter removes the key on timeout, but a node that dies mid-call cannot. Without a TTL its keys
        // stay in the store naming a node that no longer exists.
        var owners = new FakeRendezvousOwnerStore();
        var registry = CreateRegistry(owners, "node-a");

        // Act
        await registry.RegisterAsync("key-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(owners.TimeToLive("key-1") > TimeSpan.Zero);
    }

    [Fact]
    public async Task AClaim_OnANodeThatDoesNotHoldTheRendezvous_IsRefused()
    {
        // Arrange
        // The socket arrived at the wrong node. It cannot be served here — a live socket does not move between
        // processes — so the honest answer is no.
        var owners = new FakeRendezvousOwnerStore();
        var nodeA = CreateRegistry(owners, "node-a");
        var nodeB = CreateRegistry(owners, "node-b");
        await nodeA.RegisterAsync("key-1", TestContext.Current.CancellationToken);

        // Act
        var claimed = await nodeB.TryClaimAsync("key-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(claimed);
    }

    [Fact]
    public async Task AClaim_OnTheWrongNode_LeavesTheRendezvousForTheNodeThatOwnsIt()
    {
        // Arrange
        // Refusing is not enough: if the wrong node also tears the ownership down, the provider's retry — which
        // may well land on the right node — finds nothing, and one misrouted callback kills the call outright.
        var owners = new FakeRendezvousOwnerStore();
        var nodeA = CreateRegistry(owners, "node-a");
        var nodeB = CreateRegistry(owners, "node-b");
        var rendezvous = await nodeA.RegisterAsync("key-1", TestContext.Current.CancellationToken);

        // Act
        await nodeB.TryClaimAsync("key-1", TestContext.Current.CancellationToken);
        var claimed = await nodeA.TryClaimAsync("key-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(rendezvous, claimed);
    }

    [Fact]
    public async Task AKeyAnotherNodeAlreadyHolds_CannotBeRegisteredTwice()
    {
        // Arrange
        // Two nodes holding the same correlation key means the provider's callback binds to whichever wins the
        // race, and the other call sits silent. The key is unguessable, so this is a bug, not a collision.
        var owners = new FakeRendezvousOwnerStore();
        var nodeA = CreateRegistry(owners, "node-a");
        var nodeB = CreateRegistry(owners, "node-b");
        await nodeA.RegisterAsync("key-1", TestContext.Current.CancellationToken);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => nodeB.RegisterAsync("key-1", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AClaimedRendezvous_CannotBeClaimedAgain()
    {
        // Arrange
        var owners = new FakeRendezvousOwnerStore();
        var registry = CreateRegistry(owners, "node-a");
        await registry.RegisterAsync("key-1", TestContext.Current.CancellationToken);

        // Act
        await registry.TryClaimAsync("key-1", TestContext.Current.CancellationToken);
        var second = await registry.TryClaimAsync("key-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(second);
        Assert.Null(await owners.GetOwnerAsync("key-1", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Removing_ReleasesTheKey_SoTheSameCallCanBeRetried()
    {
        // Arrange
        var owners = new FakeRendezvousOwnerStore();
        var registry = CreateRegistry(owners, "node-a");
        await registry.RegisterAsync("key-1", TestContext.Current.CancellationToken);

        // Act
        await registry.RemoveAsync("key-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(await owners.GetOwnerAsync("key-1", TestContext.Current.CancellationToken));
        Assert.Null(await registry.TryClaimAsync("key-1", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WhenTheSharedStoreIsUnreachable_ACallOnASingleNodeStillConnects()
    {
        // Arrange
        // Redis going down must not take call audio down with it. Without the shared store this degrades to
        // exactly the per-node behaviour that was there before, which is correct whenever the callback comes
        // back to the node that started it — the overwhelmingly common case.
        var owners = new FakeRendezvousOwnerStore { Fail = true };
        var registry = CreateRegistry(owners, "node-a");

        // Act
        var rendezvous = await registry.RegisterAsync("key-1", TestContext.Current.CancellationToken);
        var claimed = await registry.TryClaimAsync("key-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(rendezvous, claimed);
    }

    [Fact]
    public async Task WhenTheSharedStoreIsUnreachable_ClaimingSomethingThisNodeNeverHeldStillRefuses()
    {
        // Arrange
        var owners = new FakeRendezvousOwnerStore { Fail = true };
        var registry = CreateRegistry(owners, "node-a");

        // Act & Assert
        Assert.Null(await registry.TryClaimAsync("key-1", TestContext.Current.CancellationToken));
    }

    private static DistributedWebSocketConnectionRegistry CreateRegistry(FakeRendezvousOwnerStore owners, string nodeId)
        => new(
            owners,
            nodeId,
            NullLogger<DistributedWebSocketConnectionRegistry>.Instance);
}

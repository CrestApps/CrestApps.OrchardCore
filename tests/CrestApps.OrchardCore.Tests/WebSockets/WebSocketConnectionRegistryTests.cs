using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.WebSockets.Services;

namespace CrestApps.OrchardCore.Tests.WebSockets;

public sealed class WebSocketConnectionRegistryTests
{
    [Fact]
    public async Task RegisterAsync_ThenTryClaimAsync_ReturnsSameRendezvousOnce()
    {
        // Arrange
        var registry = new InMemoryWebSocketConnectionRegistry();
        var rendezvous = await registry.RegisterAsync("key-1", TestContext.Current.CancellationToken);

        // Act
        var claimed = await registry.TryClaimAsync("key-1", TestContext.Current.CancellationToken);
        var secondClaim = await registry.TryClaimAsync("key-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(rendezvous, claimed);
        Assert.Null(secondClaim);
    }

    [Fact]
    public async Task TryClaimAsync_WithUnknownKey_ReturnsNull()
    {
        // Arrange
        var registry = new InMemoryWebSocketConnectionRegistry();

        // Act & Assert
        Assert.Null(await registry.TryClaimAsync("missing", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RegisterAsync_WithDuplicateKey_Throws()
    {
        // Arrange
        var registry = new InMemoryWebSocketConnectionRegistry();
        await registry.RegisterAsync("key-1", TestContext.Current.CancellationToken);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            registry.RegisterAsync("key-1", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RemoveAsync_PreventsLaterClaim()
    {
        // Arrange
        var registry = new InMemoryWebSocketConnectionRegistry();
        await registry.RegisterAsync("key-1", TestContext.Current.CancellationToken);

        // Act
        await registry.RemoveAsync("key-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(await registry.TryClaimAsync("key-1", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Rendezvous_CompletesConnectedTask_WhenClaimedAndCompleted()
    {
        // Arrange
        var registry = new InMemoryWebSocketConnectionRegistry();
        var rendezvous = await registry.RegisterAsync("key-1", TestContext.Current.CancellationToken);
        using var socket = new FakeWebSocket();

        // Act
        var claimed = await registry.TryClaimAsync("key-1", TestContext.Current.CancellationToken);
        Assert.NotNull(claimed);
        Assert.True(claimed.TryComplete(socket));

        // Assert
        var connected = await rendezvous.ConnectedTask;
        Assert.Same(socket, connected);
    }

    [Fact]
    public async Task Rendezvous_Abort_FaultsConnectedTask()
    {
        // Arrange
        var registry = new InMemoryWebSocketConnectionRegistry();
        var rendezvous = await registry.RegisterAsync("key-1", TestContext.Current.CancellationToken);

        // Act
        rendezvous.Abort();

        // Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await rendezvous.ConnectedTask);
    }
}

using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.ChatNotifications;

public sealed class ExternalChatRelayConnectionManagerTests
{
    private readonly ExternalChatRelayConnectionManager _manager;

    public ExternalChatRelayConnectionManagerTests()
    {
        _manager = new ExternalChatRelayConnectionManager(
            NullLogger<ExternalChatRelayConnectionManager>.Instance);
    }

    // ───────────────────────────────────────────────────────────────
    // GetOrCreateAsync
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetOrCreateAsync_CreatesAndConnectsRelay()
    {
        var relayMock = new Mock<IExternalChatRelay>();
        relayMock.Setup(r => r.IsConnectedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        relayMock
            .Setup(r => r.ConnectAsync(It.IsAny<ExternalChatRelayContext>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var context = CreateContext("session-1");

        var result = await _manager.GetOrCreateAsync("session-1", context, () => relayMock.Object, TestContext.Current.CancellationToken);

        Assert.Same(relayMock.Object, result);
        relayMock.Verify(r => r.ConnectAsync(context, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetOrCreateAsync_ReturnsCachedRelayWhenConnected()
    {
        var relayMock = new Mock<IExternalChatRelay>();
        relayMock.Setup(r => r.IsConnectedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        relayMock
            .Setup(r => r.ConnectAsync(It.IsAny<ExternalChatRelayContext>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var context = CreateContext("session-1");

        var first = await _manager.GetOrCreateAsync("session-1", context, () => relayMock.Object, TestContext.Current.CancellationToken);
        var second = await _manager.GetOrCreateAsync("session-1", context, () => throw new InvalidOperationException("Should not be called"), TestContext.Current.CancellationToken);

        Assert.Same(first, second);
    }

    [Fact]
    public async Task GetOrCreateAsync_DisposesRelayOnConnectFailure()
    {
        var relayMock = new Mock<IExternalChatRelay>();
        relayMock
            .Setup(r => r.ConnectAsync(It.IsAny<ExternalChatRelayContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Connection failed"));
        relayMock
            .Setup(r => r.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        var context = CreateContext("session-1");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _manager.GetOrCreateAsync("session-1", context, () => relayMock.Object, TestContext.Current.CancellationToken));

        relayMock.Verify(r => r.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task GetOrCreateAsync_NullSessionId_ThrowsArgumentException()
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => _manager.GetOrCreateAsync(null, CreateContext("x"), () => Mock.Of<IExternalChatRelay>(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetOrCreateAsync_NullContext_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _manager.GetOrCreateAsync("session-1", null, () => Mock.Of<IExternalChatRelay>(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetOrCreateAsync_NullFactory_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _manager.GetOrCreateAsync("session-1", CreateContext("session-1"), null, TestContext.Current.CancellationToken));
    }

    // ───────────────────────────────────────────────────────────────
    // Get
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_ReturnsRelayWhenExists()
    {
        var relayMock = new Mock<IExternalChatRelay>();
        relayMock.Setup(r => r.IsConnectedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        relayMock
            .Setup(r => r.ConnectAsync(It.IsAny<ExternalChatRelayContext>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _manager.GetOrCreateAsync("session-1", CreateContext("session-1"), () => relayMock.Object, TestContext.Current.CancellationToken);

        var result = _manager.Get("session-1");

        Assert.Same(relayMock.Object, result);
    }

    [Fact]
    public void Get_ReturnsNullWhenNotExists()
    {
        var result = _manager.Get("nonexistent");

        Assert.Null(result);
    }

    [Fact]
    public void Get_NullSessionId_ThrowsArgumentException()
    {
        Assert.ThrowsAny<ArgumentException>(() => _manager.Get(null));
    }

    // ───────────────────────────────────────────────────────────────
    // CloseAsync
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CloseAsync_DisconnectsAndDisposesRelay()
    {
        var relayMock = new Mock<IExternalChatRelay>();
        relayMock.Setup(r => r.IsConnectedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        relayMock
            .Setup(r => r.ConnectAsync(It.IsAny<ExternalChatRelayContext>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        relayMock
            .Setup(r => r.DisconnectAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        relayMock
            .Setup(r => r.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        await _manager.GetOrCreateAsync("session-1", CreateContext("session-1"), () => relayMock.Object, TestContext.Current.CancellationToken);
        await _manager.CloseAsync("session-1", TestContext.Current.CancellationToken);

        relayMock.Verify(r => r.DisconnectAsync(It.IsAny<CancellationToken>()), Times.Once);
        relayMock.Verify(r => r.DisposeAsync(), Times.Once);
        Assert.Null(_manager.Get("session-1"));
    }

    [Fact]
    public async Task CloseAsync_NonexistentSession_DoesNotThrow()
    {
        await _manager.CloseAsync("nonexistent", TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CloseAsync_NullSessionId_ThrowsArgumentException()
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _manager.CloseAsync(null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CloseAsync_DisconnectThrows_StillDisposes()
    {
        var relayMock = new Mock<IExternalChatRelay>();
        relayMock.Setup(r => r.IsConnectedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        relayMock
            .Setup(r => r.ConnectAsync(It.IsAny<ExternalChatRelayContext>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        relayMock
            .Setup(r => r.DisconnectAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Disconnect failed"));
        relayMock
            .Setup(r => r.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        await _manager.GetOrCreateAsync("session-1", CreateContext("session-1"), () => relayMock.Object, TestContext.Current.CancellationToken);
        await _manager.CloseAsync("session-1", TestContext.Current.CancellationToken);

        // The relay should still be disposed even when disconnect throws.
        relayMock.Verify(r => r.DisposeAsync(), Times.Once);
        Assert.Null(_manager.Get("session-1"));
    }

    // ───────────────────────────────────────────────────────────────
    // DisposeAsync
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task DisposeAsync_DisconnectsAndDisposesAllRelays()
    {
        var relay1 = new Mock<IExternalChatRelay>();
        relay1.Setup(r => r.IsConnectedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        relay1.Setup(r => r.ConnectAsync(It.IsAny<ExternalChatRelayContext>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        relay1.Setup(r => r.DisconnectAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        relay1.Setup(r => r.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var relay2 = new Mock<IExternalChatRelay>();
        relay2.Setup(r => r.IsConnectedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        relay2.Setup(r => r.ConnectAsync(It.IsAny<ExternalChatRelayContext>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        relay2.Setup(r => r.DisconnectAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        relay2.Setup(r => r.DisposeAsync()).Returns(ValueTask.CompletedTask);

        await _manager.GetOrCreateAsync("s1", CreateContext("s1"), () => relay1.Object, TestContext.Current.CancellationToken);
        await _manager.GetOrCreateAsync("s2", CreateContext("s2"), () => relay2.Object, TestContext.Current.CancellationToken);

        await _manager.DisposeAsync();

        relay1.Verify(r => r.DisconnectAsync(It.IsAny<CancellationToken>()), Times.Once);
        relay1.Verify(r => r.DisposeAsync(), Times.Once);
        relay2.Verify(r => r.DisconnectAsync(It.IsAny<CancellationToken>()), Times.Once);
        relay2.Verify(r => r.DisposeAsync(), Times.Once);
    }

    // ───────────────────────────────────────────────────────────────
    // Helpers
    // ───────────────────────────────────────────────────────────────

    private static ExternalChatRelayContext CreateContext(string sessionId)
    {
        return new ExternalChatRelayContext
        {
            SessionId = sessionId,
            ChatType = ChatContextType.AIChatSession,
        };
    }
}

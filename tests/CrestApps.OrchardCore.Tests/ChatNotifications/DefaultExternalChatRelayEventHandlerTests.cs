using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.ChatNotifications;

public sealed class DefaultExternalChatRelayEventHandlerTests
{
    private readonly Mock<IChatNotificationSender> _senderMock = new();
    private readonly DefaultExternalChatRelayEventHandler _handler;

    public DefaultExternalChatRelayEventHandlerTests()
    {
        _handler = new DefaultExternalChatRelayEventHandler(
            _senderMock.Object,
            new PassthroughStringLocalizer<DefaultExternalChatRelayEventHandler>(),
            NullLogger<DefaultExternalChatRelayEventHandler>.Instance);
    }

    // ───────────────────────────────────────────────────────────────
    // AgentTyping
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleEventAsync_AgentTyping_CallsShowTyping()
    {
        _senderMock
            .Setup(s => s.SendAsync("s1", ChatContextType.AIChatSession, It.IsAny<ChatNotification>()))
            .Returns(Task.CompletedTask);

        await _handler.HandleEventAsync("s1", ChatContextType.AIChatSession, new ExternalChatRelayEvent
        {
            EventType = ExternalChatRelayEventTypes.AgentTyping,
            AgentName = "Mike",
        }, TestContext.Current.CancellationToken);

        _senderMock.Verify(
            s => s.SendAsync("s1", ChatContextType.AIChatSession, It.Is<ChatNotification>(
                n => n.Id == ChatNotificationSenderExtensions.NotificationIds.Typing)),
            Times.Once);
    }

    [Fact]
    public async Task HandleEventAsync_AgentTyping_WithoutName_CallsShowTyping()
    {
        _senderMock
            .Setup(s => s.SendAsync("s1", ChatContextType.AIChatSession, It.IsAny<ChatNotification>()))
            .Returns(Task.CompletedTask);

        await _handler.HandleEventAsync("s1", ChatContextType.AIChatSession, new ExternalChatRelayEvent
        {
            EventType = ExternalChatRelayEventTypes.AgentTyping,
        }, TestContext.Current.CancellationToken);

        _senderMock.Verify(
            s => s.SendAsync("s1", ChatContextType.AIChatSession, It.Is<ChatNotification>(
                n => n.Id == ChatNotificationSenderExtensions.NotificationIds.Typing)),
            Times.Once);
    }

    // ───────────────────────────────────────────────────────────────
    // AgentStoppedTyping
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleEventAsync_AgentStoppedTyping_CallsHideTyping()
    {
        _senderMock
            .Setup(s => s.RemoveAsync("s1", ChatContextType.AIChatSession, ChatNotificationSenderExtensions.NotificationIds.Typing))
            .Returns(Task.CompletedTask);

        await _handler.HandleEventAsync("s1", ChatContextType.AIChatSession, new ExternalChatRelayEvent
        {
            EventType = ExternalChatRelayEventTypes.AgentStoppedTyping,
        }, TestContext.Current.CancellationToken);

        _senderMock.Verify(
            s => s.RemoveAsync("s1", ChatContextType.AIChatSession, ChatNotificationSenderExtensions.NotificationIds.Typing),
            Times.Once);
    }

    // ───────────────────────────────────────────────────────────────
    // AgentConnected
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleEventAsync_AgentConnected_HidesTransferAndShowsConnected()
    {
        _senderMock
            .Setup(s => s.RemoveAsync("s1", ChatContextType.AIChatSession, ChatNotificationSenderExtensions.NotificationIds.Transfer))
            .Returns(Task.CompletedTask);
        _senderMock
            .Setup(s => s.SendAsync("s1", ChatContextType.AIChatSession, It.IsAny<ChatNotification>()))
            .Returns(Task.CompletedTask);

        await _handler.HandleEventAsync("s1", ChatContextType.AIChatSession, new ExternalChatRelayEvent
        {
            EventType = ExternalChatRelayEventTypes.AgentConnected,
            AgentName = "Sarah",
        }, TestContext.Current.CancellationToken);

        _senderMock.Verify(
            s => s.RemoveAsync("s1", ChatContextType.AIChatSession, ChatNotificationSenderExtensions.NotificationIds.Transfer),
            Times.Once);
        _senderMock.Verify(
            s => s.SendAsync("s1", ChatContextType.AIChatSession, It.Is<ChatNotification>(
                n => n.Id == ChatNotificationSenderExtensions.NotificationIds.AgentConnected)),
            Times.Once);
    }

    [Fact]
    public async Task HandleEventAsync_AgentConnected_WithCustomMessage_UsesCustomContent()
    {
        ChatNotification captured = null;
        _senderMock
            .Setup(s => s.RemoveAsync("s1", ChatContextType.AIChatSession, It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _senderMock
            .Setup(s => s.SendAsync("s1", ChatContextType.AIChatSession, It.IsAny<ChatNotification>()))
            .Callback<string, ChatContextType, ChatNotification>((_, _, n) => captured = n)
            .Returns(Task.CompletedTask);

        await _handler.HandleEventAsync("s1", ChatContextType.AIChatSession, new ExternalChatRelayEvent
        {
            EventType = ExternalChatRelayEventTypes.AgentConnected,
            AgentName = "Mike",
            Content = "Agent Mike is now assisting you.",
        }, TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        Assert.Equal("Agent Mike is now assisting you.", captured.Content);
    }

    // ───────────────────────────────────────────────────────────────
    // AgentDisconnected
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleEventAsync_AgentDisconnected_HidesAgentConnected()
    {
        _senderMock
            .Setup(s => s.RemoveAsync("s1", ChatContextType.AIChatSession, ChatNotificationSenderExtensions.NotificationIds.AgentConnected))
            .Returns(Task.CompletedTask);

        await _handler.HandleEventAsync("s1", ChatContextType.AIChatSession, new ExternalChatRelayEvent
        {
            EventType = ExternalChatRelayEventTypes.AgentDisconnected,
        }, TestContext.Current.CancellationToken);

        _senderMock.Verify(
            s => s.RemoveAsync("s1", ChatContextType.AIChatSession, ChatNotificationSenderExtensions.NotificationIds.AgentConnected),
            Times.Once);
    }

    // ───────────────────────────────────────────────────────────────
    // AgentReconnecting
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleEventAsync_AgentReconnecting_ShowsReconnectingNotification()
    {
        _senderMock
            .Setup(s => s.SendAsync("s1", ChatContextType.AIChatSession, It.IsAny<ChatNotification>()))
            .Returns(Task.CompletedTask);

        await _handler.HandleEventAsync("s1", ChatContextType.AIChatSession, new ExternalChatRelayEvent
        {
            EventType = ExternalChatRelayEventTypes.AgentReconnecting,
            AgentName = "Sarah",
        }, TestContext.Current.CancellationToken);

        _senderMock.Verify(
            s => s.SendAsync("s1", ChatContextType.AIChatSession, It.Is<ChatNotification>(
                n => n.Id == ChatNotificationSenderExtensions.NotificationIds.AgentReconnecting)),
            Times.Once);
    }

    // ───────────────────────────────────────────────────────────────
    // ConnectionLost
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleEventAsync_ConnectionLost_ShowsConnectionLostNotification()
    {
        _senderMock
            .Setup(s => s.SendAsync("s1", ChatContextType.AIChatSession, It.IsAny<ChatNotification>()))
            .Returns(Task.CompletedTask);

        await _handler.HandleEventAsync("s1", ChatContextType.AIChatSession, new ExternalChatRelayEvent
        {
            EventType = ExternalChatRelayEventTypes.ConnectionLost,
        }, TestContext.Current.CancellationToken);

        _senderMock.Verify(
            s => s.SendAsync("s1", ChatContextType.AIChatSession, It.Is<ChatNotification>(
                n => n.Id == ChatNotificationSenderExtensions.NotificationIds.ConnectionLost)),
            Times.Once);
    }

    // ───────────────────────────────────────────────────────────────
    // ConnectionRestored
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleEventAsync_ConnectionRestored_HidesConnectionLostNotification()
    {
        _senderMock
            .Setup(s => s.RemoveAsync("s1", ChatContextType.AIChatSession, ChatNotificationSenderExtensions.NotificationIds.ConnectionLost))
            .Returns(Task.CompletedTask);

        await _handler.HandleEventAsync("s1", ChatContextType.AIChatSession, new ExternalChatRelayEvent
        {
            EventType = ExternalChatRelayEventTypes.ConnectionRestored,
        }, TestContext.Current.CancellationToken);

        _senderMock.Verify(
            s => s.RemoveAsync("s1", ChatContextType.AIChatSession, ChatNotificationSenderExtensions.NotificationIds.ConnectionLost),
            Times.Once);
    }

    // ───────────────────────────────────────────────────────────────
    // WaitTimeUpdated
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleEventAsync_WaitTimeUpdated_UpdatesTransfer()
    {
        _senderMock
            .Setup(s => s.SendAsync("s1", ChatContextType.AIChatSession, It.IsAny<ChatNotification>()))
            .Returns(Task.CompletedTask);

        await _handler.HandleEventAsync("s1", ChatContextType.AIChatSession, new ExternalChatRelayEvent
        {
            EventType = ExternalChatRelayEventTypes.WaitTimeUpdated,
            Content = "3 minutes",
        }, TestContext.Current.CancellationToken);

        _senderMock.Verify(
            s => s.SendAsync("s1", ChatContextType.AIChatSession, It.Is<ChatNotification>(
                n => n.Id == ChatNotificationSenderExtensions.NotificationIds.Transfer
                    && n.Metadata != null
                    && n.Metadata.ContainsKey("estimatedWaitTime"))),
            Times.Once);
    }

    // ───────────────────────────────────────────────────────────────
    // SessionEnded
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleEventAsync_SessionEnded_ShowsSessionEnded()
    {
        _senderMock
            .Setup(s => s.SendAsync("s1", ChatContextType.AIChatSession, It.IsAny<ChatNotification>()))
            .Returns(Task.CompletedTask);

        await _handler.HandleEventAsync("s1", ChatContextType.AIChatSession, new ExternalChatRelayEvent
        {
            EventType = ExternalChatRelayEventTypes.SessionEnded,
            Content = "The agent has ended the session.",
        }, TestContext.Current.CancellationToken);

        _senderMock.Verify(
            s => s.SendAsync("s1", ChatContextType.AIChatSession, It.Is<ChatNotification>(
                n => n.Id == ChatNotificationSenderExtensions.NotificationIds.SessionEnded
                    && n.Content == "The agent has ended the session.")),
            Times.Once);
    }

    [Fact]
    public async Task HandleEventAsync_SessionEnded_DefaultMessage()
    {
        _senderMock
            .Setup(s => s.SendAsync("s1", ChatContextType.AIChatSession, It.IsAny<ChatNotification>()))
            .Returns(Task.CompletedTask);

        await _handler.HandleEventAsync("s1", ChatContextType.AIChatSession, new ExternalChatRelayEvent
        {
            EventType = ExternalChatRelayEventTypes.SessionEnded,
        }, TestContext.Current.CancellationToken);

        _senderMock.Verify(
            s => s.SendAsync("s1", ChatContextType.AIChatSession, It.Is<ChatNotification>(
                n => n.Id == ChatNotificationSenderExtensions.NotificationIds.SessionEnded)),
            Times.Once);
    }

    // ───────────────────────────────────────────────────────────────
    // Message (not handled by notification sender)
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleEventAsync_Message_DoesNotCallNotificationSender()
    {
        await _handler.HandleEventAsync("s1", ChatContextType.AIChatSession, new ExternalChatRelayEvent
        {
            EventType = ExternalChatRelayEventTypes.Message,
            Content = "Hello from agent",
        }, TestContext.Current.CancellationToken);

        _senderMock.Verify(
            s => s.SendAsync(It.IsAny<string>(), It.IsAny<ChatContextType>(), It.IsAny<ChatNotification>()),
            Times.Never);
        _senderMock.Verify(
            s => s.RemoveAsync(It.IsAny<string>(), It.IsAny<ChatContextType>(), It.IsAny<string>()),
            Times.Never);
    }

    // ───────────────────────────────────────────────────────────────
    // Custom / unrecognized events (not handled by default)
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleEventAsync_CustomEventType_DoesNotCallNotificationSender()
    {
        await _handler.HandleEventAsync("s1", ChatContextType.AIChatSession, new ExternalChatRelayEvent
        {
            EventType = "thumbs-up",
        }, TestContext.Current.CancellationToken);

        _senderMock.Verify(
            s => s.SendAsync(It.IsAny<string>(), It.IsAny<ChatContextType>(), It.IsAny<ChatNotification>()),
            Times.Never);
    }

    // ───────────────────────────────────────────────────────────────
    // Validation
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleEventAsync_NullSessionId_ThrowsArgumentException()
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => _handler.HandleEventAsync(
                null, ChatContextType.AIChatSession, new ExternalChatRelayEvent(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task HandleEventAsync_NullEvent_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.HandleEventAsync(
                "s1", ChatContextType.AIChatSession, null, TestContext.Current.CancellationToken));
    }

    // ───────────────────────────────────────────────────────────────
    // ChatInteraction support
    // ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(ChatContextType.AIChatSession)]
    [InlineData(ChatContextType.ChatInteraction)]
    public async Task HandleEventAsync_AgentTyping_WorksWithBothChatTypes(ChatContextType chatType)
    {
        _senderMock
            .Setup(s => s.SendAsync("s1", chatType, It.IsAny<ChatNotification>()))
            .Returns(Task.CompletedTask);

        await _handler.HandleEventAsync("s1", chatType, new ExternalChatRelayEvent
        {
            EventType = ExternalChatRelayEventTypes.AgentTyping,
            AgentName = "Agent",
        }, TestContext.Current.CancellationToken);

        _senderMock.Verify(
            s => s.SendAsync("s1", chatType, It.Is<ChatNotification>(
                n => n.Id == ChatNotificationSenderExtensions.NotificationIds.Typing)),
            Times.Once);
    }

    // ───────────────────────────────────────────────────────────────
    // Test helpers: pass-through localizers
    // ───────────────────────────────────────────────────────────────

    private sealed class PassthroughStringLocalizer : IStringLocalizer
    {
        public LocalizedString this[string name]
            => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => [];
    }

    private sealed class PassthroughStringLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name]
            => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => [];
    }
}

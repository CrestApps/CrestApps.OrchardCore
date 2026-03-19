using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Core.Services.NotificationBuilders;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.ChatNotifications;

public sealed class DefaultExternalChatRelayEventHandlerTests
{
    private readonly Mock<IChatNotificationSender> _senderMock = new();

    private DefaultExternalChatRelayEventHandler CreateHandler(IServiceProvider serviceProvider)
    {
        return new DefaultExternalChatRelayEventHandler(
            serviceProvider,
            new DefaultExternalChatRelayNotificationHandler(_senderMock.Object),
            new PassthroughStringLocalizer<DefaultExternalChatRelayEventHandler>(),
            NullLogger<DefaultExternalChatRelayEventHandler>.Instance);
    }

    private static ServiceProvider BuildServiceProvider(params (string eventType, IExternalChatRelayNotificationBuilder builder)[] builders)
    {
        var services = new ServiceCollection();
        foreach (var (eventType, builder) in builders)
        {
            services.AddKeyedSingleton<IExternalChatRelayNotificationBuilder>(eventType, builder);
        }

        return services.BuildServiceProvider();
    }

    private static ServiceProvider BuildServiceProviderWithBuiltInBuilders()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IExternalChatRelayNotificationBuilder, AgentTypingNotificationBuilder>(ExternalChatRelayEventTypes.AgentTyping);
        services.AddKeyedSingleton<IExternalChatRelayNotificationBuilder, AgentStoppedTypingNotificationBuilder>(ExternalChatRelayEventTypes.AgentStoppedTyping);
        services.AddKeyedSingleton<IExternalChatRelayNotificationBuilder, AgentConnectedNotificationBuilder>(ExternalChatRelayEventTypes.AgentConnected);
        services.AddKeyedSingleton<IExternalChatRelayNotificationBuilder, AgentDisconnectedNotificationBuilder>(ExternalChatRelayEventTypes.AgentDisconnected);
        services.AddKeyedSingleton<IExternalChatRelayNotificationBuilder, AgentReconnectingNotificationBuilder>(ExternalChatRelayEventTypes.AgentReconnecting);
        services.AddKeyedSingleton<IExternalChatRelayNotificationBuilder, ConnectionLostNotificationBuilder>(ExternalChatRelayEventTypes.ConnectionLost);
        services.AddKeyedSingleton<IExternalChatRelayNotificationBuilder, ConnectionRestoredNotificationBuilder>(ExternalChatRelayEventTypes.ConnectionRestored);
        services.AddKeyedSingleton<IExternalChatRelayNotificationBuilder, WaitTimeUpdatedNotificationBuilder>(ExternalChatRelayEventTypes.WaitTimeUpdated);
        services.AddKeyedSingleton<IExternalChatRelayNotificationBuilder, SessionEndedNotificationBuilder>(ExternalChatRelayEventTypes.SessionEnded);

        return services.BuildServiceProvider();
    }

    // ───────────────────────────────────────────────────────────────
    // AgentTyping
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleEventAsync_AgentTyping_CallsShowTyping()
    {
        using var sp = BuildServiceProviderWithBuiltInBuilders();
        var handler = CreateHandler(sp);

        _senderMock
            .Setup(s => s.SendAsync("s1", ChatContextType.AIChatSession, It.IsAny<ChatNotification>()))
            .Returns(Task.CompletedTask);

        await handler.HandleEventAsync("s1", ChatContextType.AIChatSession, new ExternalChatRelayEvent
        {
            EventType = ExternalChatRelayEventTypes.AgentTyping,
            AgentName = "Mike",
        }, TestContext.Current.CancellationToken);

        _senderMock.Verify(
            s => s.SendAsync("s1", ChatContextType.AIChatSession, It.Is<ChatNotification>(
                n => n.Type == ChatNotificationTypes.Typing)),
            Times.Once);
    }

    [Fact]
    public async Task HandleEventAsync_AgentTyping_WithoutName_CallsShowTyping()
    {
        using var sp = BuildServiceProviderWithBuiltInBuilders();
        var handler = CreateHandler(sp);

        _senderMock
            .Setup(s => s.SendAsync("s1", ChatContextType.AIChatSession, It.IsAny<ChatNotification>()))
            .Returns(Task.CompletedTask);

        await handler.HandleEventAsync("s1", ChatContextType.AIChatSession, new ExternalChatRelayEvent
        {
            EventType = ExternalChatRelayEventTypes.AgentTyping,
        }, TestContext.Current.CancellationToken);

        _senderMock.Verify(
            s => s.SendAsync("s1", ChatContextType.AIChatSession, It.Is<ChatNotification>(
                n => n.Type == ChatNotificationTypes.Typing)),
            Times.Once);
    }

    // ───────────────────────────────────────────────────────────────
    // AgentStoppedTyping
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleEventAsync_AgentStoppedTyping_CallsHideTyping()
    {
        using var sp = BuildServiceProviderWithBuiltInBuilders();
        var handler = CreateHandler(sp);

        _senderMock
            .Setup(s => s.RemoveAsync("s1", ChatContextType.AIChatSession, ChatNotificationTypes.Typing))
            .Returns(Task.CompletedTask);

        await handler.HandleEventAsync("s1", ChatContextType.AIChatSession, new ExternalChatRelayEvent
        {
            EventType = ExternalChatRelayEventTypes.AgentStoppedTyping,
        }, TestContext.Current.CancellationToken);

        _senderMock.Verify(
            s => s.RemoveAsync("s1", ChatContextType.AIChatSession, ChatNotificationTypes.Typing),
            Times.Once);
    }

    // ───────────────────────────────────────────────────────────────
    // AgentConnected
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleEventAsync_AgentConnected_HidesTransferAndShowsConnected()
    {
        using var sp = BuildServiceProviderWithBuiltInBuilders();
        var handler = CreateHandler(sp);

        _senderMock
            .Setup(s => s.RemoveAsync("s1", ChatContextType.AIChatSession, ChatNotificationTypes.Transfer))
            .Returns(Task.CompletedTask);
        _senderMock
            .Setup(s => s.SendAsync("s1", ChatContextType.AIChatSession, It.IsAny<ChatNotification>()))
            .Returns(Task.CompletedTask);

        await handler.HandleEventAsync("s1", ChatContextType.AIChatSession, new ExternalChatRelayEvent
        {
            EventType = ExternalChatRelayEventTypes.AgentConnected,
            AgentName = "Sarah",
        }, TestContext.Current.CancellationToken);

        _senderMock.Verify(
            s => s.RemoveAsync("s1", ChatContextType.AIChatSession, ChatNotificationTypes.Transfer),
            Times.Once);
        _senderMock.Verify(
            s => s.SendAsync("s1", ChatContextType.AIChatSession, It.Is<ChatNotification>(
                n => n.Type == ChatNotificationTypes.AgentConnected)),
            Times.Once);
    }

    [Fact]
    public async Task HandleEventAsync_AgentConnected_WithCustomMessage_UsesCustomContent()
    {
        using var sp = BuildServiceProviderWithBuiltInBuilders();
        var handler = CreateHandler(sp);

        ChatNotification captured = null;
        _senderMock
            .Setup(s => s.RemoveAsync("s1", ChatContextType.AIChatSession, It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _senderMock
            .Setup(s => s.SendAsync("s1", ChatContextType.AIChatSession, It.IsAny<ChatNotification>()))
            .Callback<string, ChatContextType, ChatNotification>((_, _, n) => captured = n)
            .Returns(Task.CompletedTask);

        await handler.HandleEventAsync("s1", ChatContextType.AIChatSession, new ExternalChatRelayEvent
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
        using var sp = BuildServiceProviderWithBuiltInBuilders();
        var handler = CreateHandler(sp);

        _senderMock
            .Setup(s => s.RemoveAsync("s1", ChatContextType.AIChatSession, ChatNotificationTypes.AgentConnected))
            .Returns(Task.CompletedTask);

        await handler.HandleEventAsync("s1", ChatContextType.AIChatSession, new ExternalChatRelayEvent
        {
            EventType = ExternalChatRelayEventTypes.AgentDisconnected,
        }, TestContext.Current.CancellationToken);

        _senderMock.Verify(
            s => s.RemoveAsync("s1", ChatContextType.AIChatSession, ChatNotificationTypes.AgentConnected),
            Times.Once);
    }

    // ───────────────────────────────────────────────────────────────
    // AgentReconnecting
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleEventAsync_AgentReconnecting_ShowsReconnectingNotification()
    {
        using var sp = BuildServiceProviderWithBuiltInBuilders();
        var handler = CreateHandler(sp);

        _senderMock
            .Setup(s => s.SendAsync("s1", ChatContextType.AIChatSession, It.IsAny<ChatNotification>()))
            .Returns(Task.CompletedTask);

        await handler.HandleEventAsync("s1", ChatContextType.AIChatSession, new ExternalChatRelayEvent
        {
            EventType = ExternalChatRelayEventTypes.AgentReconnecting,
            AgentName = "Sarah",
        }, TestContext.Current.CancellationToken);

        _senderMock.Verify(
            s => s.SendAsync("s1", ChatContextType.AIChatSession, It.Is<ChatNotification>(
                n => n.Type == ChatNotificationTypes.AgentReconnecting)),
            Times.Once);
    }

    // ───────────────────────────────────────────────────────────────
    // ConnectionLost
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleEventAsync_ConnectionLost_ShowsConnectionLostNotification()
    {
        using var sp = BuildServiceProviderWithBuiltInBuilders();
        var handler = CreateHandler(sp);

        _senderMock
            .Setup(s => s.SendAsync("s1", ChatContextType.AIChatSession, It.IsAny<ChatNotification>()))
            .Returns(Task.CompletedTask);

        await handler.HandleEventAsync("s1", ChatContextType.AIChatSession, new ExternalChatRelayEvent
        {
            EventType = ExternalChatRelayEventTypes.ConnectionLost,
        }, TestContext.Current.CancellationToken);

        _senderMock.Verify(
            s => s.SendAsync("s1", ChatContextType.AIChatSession, It.Is<ChatNotification>(
                n => n.Type == ChatNotificationTypes.ConnectionLost)),
            Times.Once);
    }

    // ───────────────────────────────────────────────────────────────
    // ConnectionRestored
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleEventAsync_ConnectionRestored_HidesConnectionLostNotification()
    {
        using var sp = BuildServiceProviderWithBuiltInBuilders();
        var handler = CreateHandler(sp);

        _senderMock
            .Setup(s => s.RemoveAsync("s1", ChatContextType.AIChatSession, ChatNotificationTypes.ConnectionLost))
            .Returns(Task.CompletedTask);

        await handler.HandleEventAsync("s1", ChatContextType.AIChatSession, new ExternalChatRelayEvent
        {
            EventType = ExternalChatRelayEventTypes.ConnectionRestored,
        }, TestContext.Current.CancellationToken);

        _senderMock.Verify(
            s => s.RemoveAsync("s1", ChatContextType.AIChatSession, ChatNotificationTypes.ConnectionLost),
            Times.Once);
    }

    // ───────────────────────────────────────────────────────────────
    // WaitTimeUpdated
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleEventAsync_WaitTimeUpdated_UpdatesTransfer()
    {
        using var sp = BuildServiceProviderWithBuiltInBuilders();
        var handler = CreateHandler(sp);

        _senderMock
            .Setup(s => s.UpdateAsync("s1", ChatContextType.AIChatSession, It.IsAny<ChatNotification>()))
            .Returns(Task.CompletedTask);

        await handler.HandleEventAsync("s1", ChatContextType.AIChatSession, new ExternalChatRelayEvent
        {
            EventType = ExternalChatRelayEventTypes.WaitTimeUpdated,
            Content = "3 minutes",
        }, TestContext.Current.CancellationToken);

        _senderMock.Verify(
            s => s.UpdateAsync("s1", ChatContextType.AIChatSession, It.Is<ChatNotification>(
                n => n.Type == ChatNotificationTypes.Transfer
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
        using var sp = BuildServiceProviderWithBuiltInBuilders();
        var handler = CreateHandler(sp);

        _senderMock
            .Setup(s => s.SendAsync("s1", ChatContextType.AIChatSession, It.IsAny<ChatNotification>()))
            .Returns(Task.CompletedTask);

        await handler.HandleEventAsync("s1", ChatContextType.AIChatSession, new ExternalChatRelayEvent
        {
            EventType = ExternalChatRelayEventTypes.SessionEnded,
            Content = "The agent has ended the session.",
        }, TestContext.Current.CancellationToken);

        _senderMock.Verify(
            s => s.SendAsync("s1", ChatContextType.AIChatSession, It.Is<ChatNotification>(
                n => n.Type == ChatNotificationTypes.SessionEnded
                    && n.Content == "The agent has ended the session.")),
            Times.Once);
    }

    [Fact]
    public async Task HandleEventAsync_SessionEnded_DefaultMessage()
    {
        using var sp = BuildServiceProviderWithBuiltInBuilders();
        var handler = CreateHandler(sp);

        _senderMock
            .Setup(s => s.SendAsync("s1", ChatContextType.AIChatSession, It.IsAny<ChatNotification>()))
            .Returns(Task.CompletedTask);

        await handler.HandleEventAsync("s1", ChatContextType.AIChatSession, new ExternalChatRelayEvent
        {
            EventType = ExternalChatRelayEventTypes.SessionEnded,
        }, TestContext.Current.CancellationToken);

        _senderMock.Verify(
            s => s.SendAsync("s1", ChatContextType.AIChatSession, It.Is<ChatNotification>(
                n => n.Type == ChatNotificationTypes.SessionEnded)),
            Times.Once);
    }

    // ───────────────────────────────────────────────────────────────
    // No builder registered (unrecognized event types)
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleEventAsync_Message_DoesNotCallNotificationSender()
    {
        using var sp = BuildServiceProviderWithBuiltInBuilders();
        var handler = CreateHandler(sp);

        await handler.HandleEventAsync("s1", ChatContextType.AIChatSession, new ExternalChatRelayEvent
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

    [Fact]
    public async Task HandleEventAsync_CustomEventType_DoesNotCallNotificationSender()
    {
        using var sp = BuildServiceProviderWithBuiltInBuilders();
        var handler = CreateHandler(sp);

        await handler.HandleEventAsync("s1", ChatContextType.AIChatSession, new ExternalChatRelayEvent
        {
            EventType = "thumbs-up",
        }, TestContext.Current.CancellationToken);

        _senderMock.Verify(
            s => s.SendAsync(It.IsAny<string>(), It.IsAny<ChatContextType>(), It.IsAny<ChatNotification>()),
            Times.Never);
    }

    // ───────────────────────────────────────────────────────────────
    // Custom keyed builder
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleEventAsync_CustomBuilder_IsResolvedAndUsed()
    {
        var customBuilder = new TestCustomNotificationBuilder();
        using var sp = BuildServiceProvider(("supervisor-joined", customBuilder));
        var handler = CreateHandler(sp);

        _senderMock
            .Setup(s => s.SendAsync("s1", ChatContextType.AIChatSession, It.IsAny<ChatNotification>()))
            .Returns(Task.CompletedTask);

        await handler.HandleEventAsync("s1", ChatContextType.AIChatSession, new ExternalChatRelayEvent
        {
            EventType = "supervisor-joined",
        }, TestContext.Current.CancellationToken);

        _senderMock.Verify(
            s => s.SendAsync("s1", ChatContextType.AIChatSession, It.Is<ChatNotification>(
                n => n.Type == "info" && n.Content == "A supervisor has joined.")),
            Times.Once);
    }

    // ───────────────────────────────────────────────────────────────
    // Validation
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleEventAsync_NullSessionId_ThrowsArgumentException()
    {
        using var sp = BuildServiceProviderWithBuiltInBuilders();
        var handler = CreateHandler(sp);

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => handler.HandleEventAsync(
                null, ChatContextType.AIChatSession, new ExternalChatRelayEvent(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task HandleEventAsync_NullEvent_ThrowsArgumentNullException()
    {
        using var sp = BuildServiceProviderWithBuiltInBuilders();
        var handler = CreateHandler(sp);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => handler.HandleEventAsync(
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
        using var sp = BuildServiceProviderWithBuiltInBuilders();
        var handler = CreateHandler(sp);

        _senderMock
            .Setup(s => s.SendAsync("s1", chatType, It.IsAny<ChatNotification>()))
            .Returns(Task.CompletedTask);

        await handler.HandleEventAsync("s1", chatType, new ExternalChatRelayEvent
        {
            EventType = ExternalChatRelayEventTypes.AgentTyping,
            AgentName = "Agent",
        }, TestContext.Current.CancellationToken);

        _senderMock.Verify(
            s => s.SendAsync("s1", chatType, It.Is<ChatNotification>(
                n => n.Type == ChatNotificationTypes.Typing)),
            Times.Once);
    }

    // ───────────────────────────────────────────────────────────────
    // Test helpers
    // ───────────────────────────────────────────────────────────────

    private sealed class TestCustomNotificationBuilder : IExternalChatRelayNotificationBuilder
    {
        public string NotificationType => "info";

        public void Build(ExternalChatRelayEvent relayEvent, ChatNotification notification, ExternalChatRelayNotificationResult result, IStringLocalizer T)
        {
            // Type is set by handler via NotificationType;
            notification.Content = "A supervisor has joined.";
            notification.Icon = "fa-solid fa-user-shield";
            notification.Dismissible = true;
        }
    }

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

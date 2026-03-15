using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Chat.Services;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.ChatNotifications;

public sealed class EndSessionNotificationActionHandlerTests
{
    // ───────────────────────────────────────────────────────────────
    // AIChatSession path — session found
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_AIChatSession_ClosesSessionAndShowsSessionEndedNotification()
    {
        var now = new DateTime(2026, 3, 14, 22, 0, 0, DateTimeKind.Utc);

        var session = new AIChatSession
        {
            SessionId = "session-1",
            Status = ChatSessionStatus.Active,
        };

        var sessionManagerMock = new Mock<IAIChatSessionManager>();
        sessionManagerMock
            .Setup(m => m.FindByIdAsync("session-1"))
            .ReturnsAsync(session);
        sessionManagerMock
            .Setup(m => m.SaveAsync(session))
            .Returns(Task.CompletedTask)
            .Verifiable();

        ChatNotification captured = null;
        var senderMock = new Mock<IChatNotificationSender>();
        senderMock
            .Setup(s => s.SendAsync("session-1", ChatContextType.AIChatSession, It.IsAny<ChatNotification>()))
            .Callback<string, ChatContextType, ChatNotification>((_, _, n) => captured = n)
            .Returns(Task.CompletedTask);

        var clockMock = new Mock<IClock>();
        clockMock.Setup(c => c.UtcNow).Returns(now);

        var services = BuildServiceProvider(
            sessionManager: sessionManagerMock.Object,
            notificationSender: senderMock.Object,
            clock: clockMock.Object);

        var context = CreateContext("session-1", ChatContextType.AIChatSession, services);

        var handler = new EndSessionNotificationActionHandler();
        await handler.HandleAsync(context, CancellationToken.None);

        Assert.Equal(ChatSessionStatus.Closed, session.Status);
        Assert.Equal(now, session.ClosedAtUtc);
        sessionManagerMock.Verify();

        Assert.NotNull(captured);
        Assert.Equal(ChatNotificationSenderExtensions.NotificationIds.SessionEnded, captured.Id);
        Assert.Equal("ended", captured.Type);
        Assert.True(captured.Dismissible);
    }

    // ───────────────────────────────────────────────────────────────
    // AIChatSession path — session not found
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_AIChatSession_SessionNotFound_DoesNotSendSessionEndedNotification()
    {
        var sessionManagerMock = new Mock<IAIChatSessionManager>();
        sessionManagerMock
            .Setup(m => m.FindByIdAsync("missing"))
            .ReturnsAsync((AIChatSession)null);

        var senderMock = new Mock<IChatNotificationSender>();

        var services = BuildServiceProvider(
            sessionManager: sessionManagerMock.Object,
            notificationSender: senderMock.Object);

        var context = CreateContext("missing", ChatContextType.AIChatSession, services);

        var handler = new EndSessionNotificationActionHandler();
        await handler.HandleAsync(context, CancellationToken.None);

        // SaveAsync should not be called when session is not found.
        sessionManagerMock.Verify(m => m.SaveAsync(It.IsAny<AIChatSession>()), Times.Never);

        // Notification is not sent when session is not found (early return).
        senderMock.Verify(
            s => s.SendAsync(It.IsAny<string>(), It.IsAny<ChatContextType>(), It.IsAny<ChatNotification>()),
            Times.Never);
    }

    // ───────────────────────────────────────────────────────────────
    // ChatInteraction path — only shows notification (no close logic)
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_ChatInteraction_ShowsSessionEndedNotification()
    {
        ChatNotification captured = null;
        var senderMock = new Mock<IChatNotificationSender>();
        senderMock
            .Setup(s => s.SendAsync("i1", ChatContextType.ChatInteraction, It.IsAny<ChatNotification>()))
            .Callback<string, ChatContextType, ChatNotification>((_, _, n) => captured = n)
            .Returns(Task.CompletedTask);

        var services = BuildServiceProvider(notificationSender: senderMock.Object);

        var context = CreateContext("i1", ChatContextType.ChatInteraction, services);

        var handler = new EndSessionNotificationActionHandler();
        await handler.HandleAsync(context, CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(ChatNotificationSenderExtensions.NotificationIds.SessionEnded, captured.Id);
        Assert.Equal("ended", captured.Type);
    }

    // ───────────────────────────────────────────────────────────────
    // Helpers
    // ───────────────────────────────────────────────────────────────

    private static ChatNotificationActionContext CreateContext(
        string sessionId,
        ChatContextType chatType,
        IServiceProvider services)
    {
        return new ChatNotificationActionContext
        {
            SessionId = sessionId,
            NotificationId = ChatNotificationSenderExtensions.NotificationIds.SessionEnded,
            ActionName = ChatNotificationSenderExtensions.ActionNames.EndSession,
            ChatType = chatType,
            ConnectionId = "conn-1",
            Services = services,
        };
    }

    private static ServiceProvider BuildServiceProvider(
        IAIChatSessionManager sessionManager = null,
        IChatNotificationSender notificationSender = null,
        IClock clock = null)
    {
        var services = new ServiceCollection();

        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassthroughStringLocalizer<>));

        if (sessionManager is not null)
        {
            services.AddSingleton(sessionManager);
        }

        if (notificationSender is not null)
        {
            services.AddSingleton(notificationSender);
        }

        if (clock is not null)
        {
            services.AddSingleton(clock);
        }

        return services.BuildServiceProvider();
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

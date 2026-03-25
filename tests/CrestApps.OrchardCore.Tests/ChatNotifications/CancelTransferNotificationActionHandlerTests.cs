using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Chat.Services;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.ChatNotifications;

public sealed class CancelTransferNotificationActionHandlerTests
{
    // ───────────────────────────────────────────────────────────────
    // AIChatSession path — session found
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_AIChatSession_ResetsResponseHandlerAndRemovesTransferNotification()
    {
        var session = new AIChatSession
        {
            SessionId = "session-1",
            ResponseHandlerName = "live-agent-handler",
        };

        var sessionManagerMock = new Mock<IAIChatSessionManager>();
        sessionManagerMock
            .Setup(m => m.FindByIdAsync("session-1"))
            .ReturnsAsync(session);
        sessionManagerMock
            .Setup(m => m.SaveAsync(session))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var senderMock = new Mock<IChatNotificationSender>();
        senderMock
            .Setup(s => s.RemoveAsync("session-1", ChatContextType.AIChatSession, ChatNotificationTypes.Transfer))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var services = BuildServiceProvider(
            sessionManager: sessionManagerMock.Object,
            notificationSender: senderMock.Object);

        var context = CreateContext("session-1", ChatContextType.AIChatSession, services);

        var handler = new CancelTransferNotificationActionHandler();
        await handler.HandleAsync(context, CancellationToken.None);

        Assert.Null(session.ResponseHandlerName);
        sessionManagerMock.Verify();
        senderMock.Verify();
    }

    // ───────────────────────────────────────────────────────────────
    // AIChatSession path — session not found
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_AIChatSession_SessionNotFound_DoesNotThrow()
    {
        var sessionManagerMock = new Mock<IAIChatSessionManager>();
        sessionManagerMock
            .Setup(m => m.FindByIdAsync("missing"))
            .ReturnsAsync((AIChatSession)null);

        var senderMock = new Mock<IChatNotificationSender>();
        senderMock
            .Setup(s => s.RemoveAsync("missing", ChatContextType.AIChatSession, ChatNotificationTypes.Transfer))
            .Returns(Task.CompletedTask);

        var services = BuildServiceProvider(
            sessionManager: sessionManagerMock.Object,
            notificationSender: senderMock.Object);

        var context = CreateContext("missing", ChatContextType.AIChatSession, services);

        var handler = new CancelTransferNotificationActionHandler();

        // Should not throw.
        await handler.HandleAsync(context, CancellationToken.None);

        // SaveAsync should never be called.
        sessionManagerMock.Verify(m => m.SaveAsync(It.IsAny<AIChatSession>()), Times.Never);
    }

    // ───────────────────────────────────────────────────────────────
    // ChatInteraction path — interaction found
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_ChatInteraction_ResetsResponseHandlerAndRemovesTransferNotification()
    {
        var interaction = new ChatInteraction
        {
            ResponseHandlerName = "live-agent-handler",
        };

        var interactionManagerMock = new Mock<ICatalogManager<ChatInteraction>>();
        interactionManagerMock
            .Setup(m => m.FindByIdAsync("interaction-1"))
            .ReturnsAsync(interaction);
        interactionManagerMock
            .Setup(m => m.UpdateAsync(interaction, null))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var senderMock = new Mock<IChatNotificationSender>();
        senderMock
            .Setup(s => s.RemoveAsync("interaction-1", ChatContextType.ChatInteraction, ChatNotificationTypes.Transfer))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var services = BuildServiceProvider(
            interactionManager: interactionManagerMock.Object,
            notificationSender: senderMock.Object);

        var context = CreateContext("interaction-1", ChatContextType.ChatInteraction, services);

        var handler = new CancelTransferNotificationActionHandler();
        await handler.HandleAsync(context, CancellationToken.None);

        Assert.Null(interaction.ResponseHandlerName);
        interactionManagerMock.Verify();
        senderMock.Verify();
    }

    // ───────────────────────────────────────────────────────────────
    // ChatInteraction path — interaction not found
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_ChatInteraction_InteractionNotFound_DoesNotThrow()
    {
        var interactionManagerMock = new Mock<ICatalogManager<ChatInteraction>>();
        interactionManagerMock
            .Setup(m => m.FindByIdAsync("missing"))
            .ReturnsAsync((ChatInteraction)null);

        var senderMock = new Mock<IChatNotificationSender>();
        senderMock
            .Setup(s => s.RemoveAsync("missing", ChatContextType.ChatInteraction, ChatNotificationTypes.Transfer))
            .Returns(Task.CompletedTask);

        var services = BuildServiceProvider(
            interactionManager: interactionManagerMock.Object,
            notificationSender: senderMock.Object);

        var context = CreateContext("missing", ChatContextType.ChatInteraction, services);

        var handler = new CancelTransferNotificationActionHandler();

        // Should not throw.
        await handler.HandleAsync(context, CancellationToken.None);

        // UpdateAsync should never be called.
        interactionManagerMock.Verify(m => m.UpdateAsync(It.IsAny<ChatInteraction>(), null), Times.Never);
    }

    // ───────────────────────────────────────────────────────────────
    // Transfer notification is always removed regardless of path
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_AIChatSession_SessionNotFound_DoesNotRemoveTransferNotification()
    {
        var sessionManagerMock = new Mock<IAIChatSessionManager>();
        sessionManagerMock
            .Setup(m => m.FindByIdAsync("s1"))
            .ReturnsAsync((AIChatSession)null);

        var senderMock = new Mock<IChatNotificationSender>();

        var services = BuildServiceProvider(
            sessionManager: sessionManagerMock.Object,
            notificationSender: senderMock.Object);

        var context = CreateContext("s1", ChatContextType.AIChatSession, services);

        var handler = new CancelTransferNotificationActionHandler();
        await handler.HandleAsync(context, CancellationToken.None);

        // The handler returns early when the session is not found, so
        // RemoveAsync should NOT be called.
        senderMock.Verify(
            s => s.RemoveAsync(It.IsAny<string>(), It.IsAny<ChatContextType>(), It.IsAny<string>()),
            Times.Never);
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
            NotificationType = ChatNotificationTypes.Transfer,
            ActionName = ChatNotificationActionNames.CancelTransfer,
            ChatType = chatType,
            ConnectionId = "conn-1",
            Services = services,
        };
    }

    private static ServiceProvider BuildServiceProvider(
        IAIChatSessionManager sessionManager = null,
        ICatalogManager<ChatInteraction> interactionManager = null,
        IChatNotificationSender notificationSender = null)
    {
        var services = new ServiceCollection();

        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        if (sessionManager is not null)
        {
            services.AddSingleton(sessionManager);
        }

        if (interactionManager is not null)
        {
            services.AddSingleton(interactionManager);
        }

        if (notificationSender is not null)
        {
            services.AddSingleton(notificationSender);
        }

        return services.BuildServiceProvider();
    }
}

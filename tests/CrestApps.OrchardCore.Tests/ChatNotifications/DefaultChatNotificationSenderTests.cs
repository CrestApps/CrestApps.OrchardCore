using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Chat.Services;
using CrestApps.Core.AI.Models;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.ChatNotifications;

public sealed class DefaultChatNotificationSenderTests
{
    // ───────────────────────────────────────────────────────────────
    // SendAsync
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task SendAsync_AIChatSession_DelegatesToTransport()
    {
        var notification = new ChatNotification("info") { Content = "Hello" };

        var transportMock = new Mock<IChatNotificationTransport>();
        transportMock
            .Setup(t => t.SendNotificationAsync("s1", notification))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var sender = CreateSender(ChatContextType.AIChatSession, transportMock.Object);

        await sender.SendAsync("s1", ChatContextType.AIChatSession, notification);

        transportMock.Verify();
    }

    [Fact]
    public async Task SendAsync_ChatInteraction_DelegatesToTransport()
    {
        var notification = new ChatNotification("info") { Content = "Hello" };

        var transportMock = new Mock<IChatNotificationTransport>();
        transportMock
            .Setup(t => t.SendNotificationAsync("i1", notification))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var sender = CreateSender(ChatContextType.ChatInteraction, transportMock.Object);

        await sender.SendAsync("i1", ChatContextType.ChatInteraction, notification);

        transportMock.Verify();
    }

    [Fact]
    public async Task SendAsync_NullSessionId_ThrowsArgumentException()
    {
        var sender = CreateSenderWithNoOpTransport();

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => sender.SendAsync(null, ChatContextType.AIChatSession, new ChatNotification("info")));
    }

    [Fact]
    public async Task SendAsync_NullNotification_ThrowsArgumentNullException()
    {
        var sender = CreateSenderWithNoOpTransport();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => sender.SendAsync("s1", ChatContextType.AIChatSession, null));
    }

    // ───────────────────────────────────────────────────────────────
    // UpdateAsync
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_AIChatSession_DelegatesToTransport()
    {
        var notification = new ChatNotification("info") { Content = "Updated" };

        var transportMock = new Mock<IChatNotificationTransport>();
        transportMock
            .Setup(t => t.UpdateNotificationAsync("s1", notification))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var sender = CreateSender(ChatContextType.AIChatSession, transportMock.Object);

        await sender.UpdateAsync("s1", ChatContextType.AIChatSession, notification);

        transportMock.Verify();
    }

    [Fact]
    public async Task UpdateAsync_NullSessionId_ThrowsArgumentException()
    {
        var sender = CreateSenderWithNoOpTransport();

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => sender.UpdateAsync(null, ChatContextType.AIChatSession, new ChatNotification("info")));
    }

    [Fact]
    public async Task UpdateAsync_NullNotification_ThrowsArgumentNullException()
    {
        var sender = CreateSenderWithNoOpTransport();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => sender.UpdateAsync("s1", ChatContextType.AIChatSession, null));
    }

    // ───────────────────────────────────────────────────────────────
    // RemoveAsync
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveAsync_AIChatSession_DelegatesToTransport()
    {
        var transportMock = new Mock<IChatNotificationTransport>();
        transportMock
            .Setup(t => t.RemoveNotificationAsync("s1", "typing"))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var sender = CreateSender(ChatContextType.AIChatSession, transportMock.Object);

        await sender.RemoveAsync("s1", ChatContextType.AIChatSession, "typing");

        transportMock.Verify();
    }

    [Fact]
    public async Task RemoveAsync_ChatInteraction_DelegatesToTransport()
    {
        var transportMock = new Mock<IChatNotificationTransport>();
        transportMock
            .Setup(t => t.RemoveNotificationAsync("i1", "transfer"))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var sender = CreateSender(ChatContextType.ChatInteraction, transportMock.Object);

        await sender.RemoveAsync("i1", ChatContextType.ChatInteraction, "transfer");

        transportMock.Verify();
    }

    [Fact]
    public async Task RemoveAsync_NullSessionId_ThrowsArgumentException()
    {
        var sender = CreateSenderWithNoOpTransport();

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => sender.RemoveAsync(null, ChatContextType.AIChatSession, "typing"));
    }

    [Fact]
    public async Task RemoveAsync_EmptyNotificationType_ThrowsArgumentException()
    {
        var sender = CreateSenderWithNoOpTransport();

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => sender.RemoveAsync("s1", ChatContextType.AIChatSession, ""));
    }

    // ───────────────────────────────────────────────────────────────
    // Transport resolution
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task SendAsync_NoTransportRegistered_ThrowsInvalidOperationException()
    {
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var sender = new DefaultChatNotificationSender(serviceProvider);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sender.SendAsync("s1", ChatContextType.AIChatSession, new ChatNotification("info")));
    }

    // ───────────────────────────────────────────────────────────────
    // Helpers
    // ───────────────────────────────────────────────────────────────

    private static DefaultChatNotificationSender CreateSender(
        ChatContextType chatType,
        IChatNotificationTransport transport)
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton(chatType, transport);

        return new DefaultChatNotificationSender(services.BuildServiceProvider());
    }

    private static DefaultChatNotificationSender CreateSenderWithNoOpTransport()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IChatNotificationTransport>(
            ChatContextType.AIChatSession,
            Mock.Of<IChatNotificationTransport>());

        return new DefaultChatNotificationSender(services.BuildServiceProvider());
    }
}

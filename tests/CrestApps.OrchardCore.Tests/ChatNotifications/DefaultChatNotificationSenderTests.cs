using CrestApps.OrchardCore.AI.Chat.Core.Hubs;
using CrestApps.OrchardCore.AI.Chat.Hubs;
using CrestApps.OrchardCore.AI.Chat.Interactions.Hubs;
using CrestApps.OrchardCore.AI.Chat.Services;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.SignalR;
using Moq;

namespace CrestApps.OrchardCore.Tests.ChatNotifications;

public sealed class DefaultChatNotificationSenderTests
{
    // ───────────────────────────────────────────────────────────────
    // SendAsync
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task SendAsync_AIChatSession_CallsReceiveNotificationOnCorrectGroup()
    {
        var notification = new ChatNotification { Id = "test", Type = "info", Content = "Hello" };
        var clientMock = new Mock<IAIChatHubClient>();
        clientMock
            .Setup(c => c.ReceiveNotification(notification))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var chatHubContext = CreateChatHubContext(
            AIChatHub.GetSessionGroupName("s1"),
            clientMock.Object);

        var interactionHubContext = CreateInteractionHubContext("unused", Mock.Of<IChatInteractionHubClient>());

        var sender = new DefaultChatNotificationSender(chatHubContext, interactionHubContext);

        await sender.SendAsync("s1", ChatContextType.AIChatSession, notification);

        clientMock.Verify();
    }

    [Fact]
    public async Task SendAsync_ChatInteraction_CallsReceiveNotificationOnCorrectGroup()
    {
        var notification = new ChatNotification { Id = "test", Type = "info", Content = "Hello" };
        var clientMock = new Mock<IChatInteractionHubClient>();
        clientMock
            .Setup(c => c.ReceiveNotification(notification))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var chatHubContext = CreateChatHubContext("unused", Mock.Of<IAIChatHubClient>());

        var interactionHubContext = CreateInteractionHubContext(
            ChatInteractionHub.GetInteractionGroupName("i1"),
            clientMock.Object);

        var sender = new DefaultChatNotificationSender(chatHubContext, interactionHubContext);

        await sender.SendAsync("i1", ChatContextType.ChatInteraction, notification);

        clientMock.Verify();
    }

    [Fact]
    public async Task SendAsync_NullSessionId_ThrowsArgumentException()
    {
        var sender = CreateSender();

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => sender.SendAsync(null, ChatContextType.AIChatSession, new ChatNotification()));
    }

    [Fact]
    public async Task SendAsync_NullNotification_ThrowsArgumentNullException()
    {
        var sender = CreateSender();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => sender.SendAsync("s1", ChatContextType.AIChatSession, null));
    }

    // ───────────────────────────────────────────────────────────────
    // UpdateAsync
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_AIChatSession_CallsUpdateNotificationOnCorrectGroup()
    {
        var notification = new ChatNotification { Id = "test", Type = "info", Content = "Updated" };
        var clientMock = new Mock<IAIChatHubClient>();
        clientMock
            .Setup(c => c.UpdateNotification(notification))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var chatHubContext = CreateChatHubContext(
            AIChatHub.GetSessionGroupName("s1"),
            clientMock.Object);

        var sender = new DefaultChatNotificationSender(
            chatHubContext,
            CreateInteractionHubContext("unused", Mock.Of<IChatInteractionHubClient>()));

        await sender.UpdateAsync("s1", ChatContextType.AIChatSession, notification);

        clientMock.Verify();
    }

    [Fact]
    public async Task UpdateAsync_NullSessionId_ThrowsArgumentException()
    {
        var sender = CreateSender();

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => sender.UpdateAsync(null, ChatContextType.AIChatSession, new ChatNotification()));
    }

    [Fact]
    public async Task UpdateAsync_NullNotification_ThrowsArgumentNullException()
    {
        var sender = CreateSender();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => sender.UpdateAsync("s1", ChatContextType.AIChatSession, null));
    }

    // ───────────────────────────────────────────────────────────────
    // RemoveAsync
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveAsync_AIChatSession_CallsRemoveNotificationOnCorrectGroup()
    {
        var clientMock = new Mock<IAIChatHubClient>();
        clientMock
            .Setup(c => c.RemoveNotification("typing"))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var chatHubContext = CreateChatHubContext(
            AIChatHub.GetSessionGroupName("s1"),
            clientMock.Object);

        var sender = new DefaultChatNotificationSender(
            chatHubContext,
            CreateInteractionHubContext("unused", Mock.Of<IChatInteractionHubClient>()));

        await sender.RemoveAsync("s1", ChatContextType.AIChatSession, "typing");

        clientMock.Verify();
    }

    [Fact]
    public async Task RemoveAsync_ChatInteraction_CallsRemoveNotificationOnCorrectGroup()
    {
        var clientMock = new Mock<IChatInteractionHubClient>();
        clientMock
            .Setup(c => c.RemoveNotification("transfer"))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var interactionHubContext = CreateInteractionHubContext(
            ChatInteractionHub.GetInteractionGroupName("i1"),
            clientMock.Object);

        var sender = new DefaultChatNotificationSender(
            CreateChatHubContext("unused", Mock.Of<IAIChatHubClient>()),
            interactionHubContext);

        await sender.RemoveAsync("i1", ChatContextType.ChatInteraction, "transfer");

        clientMock.Verify();
    }

    [Fact]
    public async Task RemoveAsync_NullSessionId_ThrowsArgumentException()
    {
        var sender = CreateSender();

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => sender.RemoveAsync(null, ChatContextType.AIChatSession, "typing"));
    }

    [Fact]
    public async Task RemoveAsync_EmptyNotificationId_ThrowsArgumentException()
    {
        var sender = CreateSender();

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => sender.RemoveAsync("s1", ChatContextType.AIChatSession, ""));
    }

    // ───────────────────────────────────────────────────────────────
    // Helpers
    // ───────────────────────────────────────────────────────────────

    private static DefaultChatNotificationSender CreateSender()
    {
        return new DefaultChatNotificationSender(
            CreateChatHubContext("any", Mock.Of<IAIChatHubClient>()),
            CreateInteractionHubContext("any", Mock.Of<IChatInteractionHubClient>()));
    }

    private static IHubContext<AIChatHub, IAIChatHubClient> CreateChatHubContext(
        string expectedGroupName,
        IAIChatHubClient client)
    {
        var hubClientsMock = new Mock<IHubClients<IAIChatHubClient>>();
        hubClientsMock
            .Setup(c => c.Group(expectedGroupName))
            .Returns(client);

        var contextMock = new Mock<IHubContext<AIChatHub, IAIChatHubClient>>();
        contextMock.Setup(c => c.Clients).Returns(hubClientsMock.Object);

        return contextMock.Object;
    }

    private static IHubContext<ChatInteractionHub, IChatInteractionHubClient> CreateInteractionHubContext(
        string expectedGroupName,
        IChatInteractionHubClient client)
    {
        var hubClientsMock = new Mock<IHubClients<IChatInteractionHubClient>>();
        hubClientsMock
            .Setup(c => c.Group(expectedGroupName))
            .Returns(client);

        var contextMock = new Mock<IHubContext<ChatInteractionHub, IChatInteractionHubClient>>();
        contextMock.Setup(c => c.Clients).Returns(hubClientsMock.Object);

        return contextMock.Object;
    }
}

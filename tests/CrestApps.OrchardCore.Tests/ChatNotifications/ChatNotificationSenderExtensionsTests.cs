using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Models;
using Moq;

namespace CrestApps.OrchardCore.Tests.ChatNotifications;

public sealed class ChatNotificationSenderExtensionsTests
{
    private readonly Mock<IChatNotificationSender> _senderMock = new();

    // ───────────────────────────────────────────────────────────────
    // ShowTypingAsync
    // ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(ChatContextType.AIChatSession)]
    [InlineData(ChatContextType.ChatInteraction)]
    public async Task ShowTypingAsync_WithoutAgentName_SendsDefaultContent(ChatContextType chatType)
    {
        ChatNotification captured = null;
        _senderMock
            .Setup(s => s.SendAsync(It.IsAny<string>(), chatType, It.IsAny<ChatNotification>()))
            .Callback<string, ChatContextType, ChatNotification>((_, _, n) => captured = n)
            .Returns(Task.CompletedTask);

        await _senderMock.Object.ShowTypingAsync("session-1", chatType);

        Assert.NotNull(captured);
        Assert.Equal(ChatNotificationSenderExtensions.NotificationIds.Typing, captured.Id);
        Assert.Equal("typing", captured.Type);
        Assert.Equal("Agent is typing", captured.Content);
        Assert.Equal("fa-solid fa-ellipsis", captured.Icon);
    }

    [Fact]
    public async Task ShowTypingAsync_WithAgentName_IncludesNameInContent()
    {
        ChatNotification captured = null;
        _senderMock
            .Setup(s => s.SendAsync("s1", ChatContextType.AIChatSession, It.IsAny<ChatNotification>()))
            .Callback<string, ChatContextType, ChatNotification>((_, _, n) => captured = n)
            .Returns(Task.CompletedTask);

        await _senderMock.Object.ShowTypingAsync("s1", ChatContextType.AIChatSession, "Mike");

        Assert.NotNull(captured);
        Assert.Equal("Mike is typing", captured.Content);
    }

    // ───────────────────────────────────────────────────────────────
    // HideTypingAsync
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task HideTypingAsync_CallsRemoveWithTypingId()
    {
        _senderMock
            .Setup(s => s.RemoveAsync("s1", ChatContextType.AIChatSession, ChatNotificationSenderExtensions.NotificationIds.Typing))
            .Returns(Task.CompletedTask)
            .Verifiable();

        await _senderMock.Object.HideTypingAsync("s1", ChatContextType.AIChatSession);

        _senderMock.Verify();
    }

    // ───────────────────────────────────────────────────────────────
    // ShowTransferAsync
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ShowTransferAsync_DefaultParameters_SendsTransferNotification()
    {
        ChatNotification captured = null;
        _senderMock
            .Setup(s => s.SendAsync("s1", ChatContextType.AIChatSession, It.IsAny<ChatNotification>()))
            .Callback<string, ChatContextType, ChatNotification>((_, _, n) => captured = n)
            .Returns(Task.CompletedTask);

        await _senderMock.Object.ShowTransferAsync("s1", ChatContextType.AIChatSession);

        Assert.NotNull(captured);
        Assert.Equal(ChatNotificationSenderExtensions.NotificationIds.Transfer, captured.Id);
        Assert.Equal("transfer", captured.Type);
        Assert.Equal("Transferring you to a live agent...", captured.Content);
        Assert.Equal("fa-solid fa-headset", captured.Icon);
        Assert.Single(captured.Actions);
        Assert.Equal(ChatNotificationSenderExtensions.ActionNames.CancelTransfer, captured.Actions[0].Name);
        Assert.Equal("Cancel Transfer", captured.Actions[0].Label);
    }

    [Fact]
    public async Task ShowTransferAsync_WithEstimatedWaitTime_AppendsWaitTimeToContent()
    {
        ChatNotification captured = null;
        _senderMock
            .Setup(s => s.SendAsync("s1", ChatContextType.ChatInteraction, It.IsAny<ChatNotification>()))
            .Callback<string, ChatContextType, ChatNotification>((_, _, n) => captured = n)
            .Returns(Task.CompletedTask);

        await _senderMock.Object.ShowTransferAsync("s1", ChatContextType.ChatInteraction,
            estimatedWaitTime: "2 minutes");

        Assert.NotNull(captured);
        Assert.Contains("Estimated wait: 2 minutes.", captured.Content);
        Assert.NotNull(captured.Metadata);
        Assert.Equal("2 minutes", captured.Metadata["estimatedWaitTime"]);
    }

    [Fact]
    public async Task ShowTransferAsync_WithCustomMessage_UsesCustomMessage()
    {
        ChatNotification captured = null;
        _senderMock
            .Setup(s => s.SendAsync("s1", ChatContextType.AIChatSession, It.IsAny<ChatNotification>()))
            .Callback<string, ChatContextType, ChatNotification>((_, _, n) => captured = n)
            .Returns(Task.CompletedTask);

        await _senderMock.Object.ShowTransferAsync("s1", ChatContextType.AIChatSession,
            message: "Connecting you to support...");

        Assert.NotNull(captured);
        Assert.StartsWith("Connecting you to support...", captured.Content);
    }

    [Fact]
    public async Task ShowTransferAsync_NotCancellable_HasNoActions()
    {
        ChatNotification captured = null;
        _senderMock
            .Setup(s => s.SendAsync("s1", ChatContextType.AIChatSession, It.IsAny<ChatNotification>()))
            .Callback<string, ChatContextType, ChatNotification>((_, _, n) => captured = n)
            .Returns(Task.CompletedTask);

        await _senderMock.Object.ShowTransferAsync("s1", ChatContextType.AIChatSession, cancellable: false);

        Assert.NotNull(captured);
        Assert.Null(captured.Actions);
    }

    // ───────────────────────────────────────────────────────────────
    // HideTransferAsync
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task HideTransferAsync_CallsRemoveWithTransferId()
    {
        _senderMock
            .Setup(s => s.RemoveAsync("s1", ChatContextType.ChatInteraction, ChatNotificationSenderExtensions.NotificationIds.Transfer))
            .Returns(Task.CompletedTask)
            .Verifiable();

        await _senderMock.Object.HideTransferAsync("s1", ChatContextType.ChatInteraction);

        _senderMock.Verify();
    }

    // ───────────────────────────────────────────────────────────────
    // ShowConversationEndedAsync
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ShowConversationEndedAsync_DefaultMessage_SendsEndedNotification()
    {
        ChatNotification captured = null;
        _senderMock
            .Setup(s => s.SendAsync("s1", ChatContextType.AIChatSession, It.IsAny<ChatNotification>()))
            .Callback<string, ChatContextType, ChatNotification>((_, _, n) => captured = n)
            .Returns(Task.CompletedTask);

        await _senderMock.Object.ShowConversationEndedAsync("s1", ChatContextType.AIChatSession);

        Assert.NotNull(captured);
        Assert.Equal(ChatNotificationSenderExtensions.NotificationIds.ConversationEnded, captured.Id);
        Assert.Equal("ended", captured.Type);
        Assert.Equal("Conversation ended.", captured.Content);
        Assert.True(captured.Dismissible);
    }

    [Fact]
    public async Task ShowConversationEndedAsync_CustomMessage_UsesCustomMessage()
    {
        ChatNotification captured = null;
        _senderMock
            .Setup(s => s.SendAsync("s1", ChatContextType.AIChatSession, It.IsAny<ChatNotification>()))
            .Callback<string, ChatContextType, ChatNotification>((_, _, n) => captured = n)
            .Returns(Task.CompletedTask);

        await _senderMock.Object.ShowConversationEndedAsync("s1", ChatContextType.AIChatSession,
            "Your session is over.");

        Assert.NotNull(captured);
        Assert.Equal("Your session is over.", captured.Content);
    }

    // ───────────────────────────────────────────────────────────────
    // ShowSessionEndedAsync
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ShowSessionEndedAsync_DefaultMessage_SendsSessionEndedNotification()
    {
        ChatNotification captured = null;
        _senderMock
            .Setup(s => s.SendAsync("s1", ChatContextType.AIChatSession, It.IsAny<ChatNotification>()))
            .Callback<string, ChatContextType, ChatNotification>((_, _, n) => captured = n)
            .Returns(Task.CompletedTask);

        await _senderMock.Object.ShowSessionEndedAsync("s1", ChatContextType.AIChatSession);

        Assert.NotNull(captured);
        Assert.Equal(ChatNotificationSenderExtensions.NotificationIds.SessionEnded, captured.Id);
        Assert.Equal("ended", captured.Type);
        Assert.Equal("This chat session has ended.", captured.Content);
        Assert.True(captured.Dismissible);
    }

    // ───────────────────────────────────────────────────────────────
    // UpdateTransferAsync delegates to ShowTransferAsync
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateTransferAsync_DelegatesToShowTransferAsync()
    {
        ChatNotification captured = null;
        _senderMock
            .Setup(s => s.SendAsync("s1", ChatContextType.AIChatSession, It.IsAny<ChatNotification>()))
            .Callback<string, ChatContextType, ChatNotification>((_, _, n) => captured = n)
            .Returns(Task.CompletedTask);

        await _senderMock.Object.UpdateTransferAsync("s1", ChatContextType.AIChatSession,
            message: "Still waiting...", estimatedWaitTime: "5 minutes");

        Assert.NotNull(captured);
        Assert.Equal(ChatNotificationSenderExtensions.NotificationIds.Transfer, captured.Id);
        Assert.Contains("Still waiting...", captured.Content);
        Assert.Contains("Estimated wait: 5 minutes.", captured.Content);
    }

    // ───────────────────────────────────────────────────────────────
    // Well-known constants
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void NotificationIds_AreExpectedValues()
    {
        Assert.Equal("typing", ChatNotificationSenderExtensions.NotificationIds.Typing);
        Assert.Equal("transfer", ChatNotificationSenderExtensions.NotificationIds.Transfer);
        Assert.Equal("conversation-ended", ChatNotificationSenderExtensions.NotificationIds.ConversationEnded);
        Assert.Equal("session-ended", ChatNotificationSenderExtensions.NotificationIds.SessionEnded);
    }

    [Fact]
    public void ActionNames_AreExpectedValues()
    {
        Assert.Equal("cancel-transfer", ChatNotificationSenderExtensions.ActionNames.CancelTransfer);
        Assert.Equal("end-session", ChatNotificationSenderExtensions.ActionNames.EndSession);
    }
}

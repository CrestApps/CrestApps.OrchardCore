using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Localization;
using Moq;

namespace CrestApps.OrchardCore.Tests.ChatNotifications;

public sealed class ChatNotificationSenderExtensionsTests
{
    private readonly Mock<IChatNotificationSender> _senderMock = new();
    private readonly IStringLocalizer _localizer = new PassthroughStringLocalizer();

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

        await _senderMock.Object.ShowTypingAsync("session-1", chatType, _localizer);

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

        await _senderMock.Object.ShowTypingAsync("s1", ChatContextType.AIChatSession, _localizer, "Mike");

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

        await _senderMock.Object.ShowTransferAsync("s1", ChatContextType.AIChatSession, _localizer);

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

        await _senderMock.Object.ShowTransferAsync("s1", ChatContextType.ChatInteraction, _localizer,
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

        await _senderMock.Object.ShowTransferAsync("s1", ChatContextType.AIChatSession, _localizer,
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

        await _senderMock.Object.ShowTransferAsync("s1", ChatContextType.AIChatSession, _localizer, cancellable: false);

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
    // ShowAgentConnectedAsync
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ShowAgentConnectedAsync_WithoutAgentName_SendsDefaultContent()
    {
        ChatNotification captured = null;
        _senderMock
            .Setup(s => s.SendAsync("s1", ChatContextType.AIChatSession, It.IsAny<ChatNotification>()))
            .Callback<string, ChatContextType, ChatNotification>((_, _, n) => captured = n)
            .Returns(Task.CompletedTask);

        await _senderMock.Object.ShowAgentConnectedAsync("s1", ChatContextType.AIChatSession, _localizer);

        Assert.NotNull(captured);
        Assert.Equal(ChatNotificationSenderExtensions.NotificationIds.AgentConnected, captured.Id);
        Assert.Equal("info", captured.Type);
        Assert.Equal("You are now connected to a live agent.", captured.Content);
        Assert.Equal("fa-solid fa-user-check", captured.Icon);
        Assert.True(captured.Dismissible);
    }

    [Fact]
    public async Task ShowAgentConnectedAsync_WithAgentName_IncludesNameInContent()
    {
        ChatNotification captured = null;
        _senderMock
            .Setup(s => s.SendAsync("s1", ChatContextType.AIChatSession, It.IsAny<ChatNotification>()))
            .Callback<string, ChatContextType, ChatNotification>((_, _, n) => captured = n)
            .Returns(Task.CompletedTask);

        await _senderMock.Object.ShowAgentConnectedAsync("s1", ChatContextType.AIChatSession, _localizer, agentName: "Sarah");

        Assert.NotNull(captured);
        Assert.Equal("You are now connected to Sarah.", captured.Content);
    }

    [Fact]
    public async Task ShowAgentConnectedAsync_WithCustomMessage_UsesCustomMessage()
    {
        ChatNotification captured = null;
        _senderMock
            .Setup(s => s.SendAsync("s1", ChatContextType.AIChatSession, It.IsAny<ChatNotification>()))
            .Callback<string, ChatContextType, ChatNotification>((_, _, n) => captured = n)
            .Returns(Task.CompletedTask);

        await _senderMock.Object.ShowAgentConnectedAsync("s1", ChatContextType.AIChatSession, _localizer,
            message: "Agent Mike has joined the chat.");

        Assert.NotNull(captured);
        Assert.Equal("Agent Mike has joined the chat.", captured.Content);
    }

    [Fact]
    public async Task HideAgentConnectedAsync_CallsRemoveWithAgentConnectedId()
    {
        _senderMock
            .Setup(s => s.RemoveAsync("s1", ChatContextType.AIChatSession, ChatNotificationSenderExtensions.NotificationIds.AgentConnected))
            .Returns(Task.CompletedTask)
            .Verifiable();

        await _senderMock.Object.HideAgentConnectedAsync("s1", ChatContextType.AIChatSession);

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

        await _senderMock.Object.ShowConversationEndedAsync("s1", ChatContextType.AIChatSession, _localizer);

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

        await _senderMock.Object.ShowConversationEndedAsync("s1", ChatContextType.AIChatSession, _localizer,
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

        await _senderMock.Object.ShowSessionEndedAsync("s1", ChatContextType.AIChatSession, _localizer);

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

        await _senderMock.Object.UpdateTransferAsync("s1", ChatContextType.AIChatSession, _localizer,
            estimatedWaitTime: "5 minutes");

        Assert.NotNull(captured);
        Assert.Equal(ChatNotificationSenderExtensions.NotificationIds.Transfer, captured.Id);
        Assert.Contains("Estimated wait: 5 minutes.", captured.Content);
        Assert.Contains("Transferring you to a live agent...", captured.Content);
    }

    // ───────────────────────────────────────────────────────────────
    // Well-known constants
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void NotificationIds_AreExpectedValues()
    {
        Assert.Equal("typing", ChatNotificationSenderExtensions.NotificationIds.Typing);
        Assert.Equal("transfer", ChatNotificationSenderExtensions.NotificationIds.Transfer);
        Assert.Equal("agent-connected", ChatNotificationSenderExtensions.NotificationIds.AgentConnected);
        Assert.Equal("conversation-ended", ChatNotificationSenderExtensions.NotificationIds.ConversationEnded);
        Assert.Equal("session-ended", ChatNotificationSenderExtensions.NotificationIds.SessionEnded);
    }

    [Fact]
    public void ActionNames_AreExpectedValues()
    {
        Assert.Equal("cancel-transfer", ChatNotificationSenderExtensions.ActionNames.CancelTransfer);
        Assert.Equal("end-session", ChatNotificationSenderExtensions.ActionNames.EndSession);
    }

    // ───────────────────────────────────────────────────────────────
    // Test helper: pass-through localizer that formats strings like
    // IStringLocalizer does when no translation is found.
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
}

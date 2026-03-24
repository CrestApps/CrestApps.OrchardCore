using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Core;

public sealed class ChatResponseHandlerContextTests
{
    [Fact]
    public void AssistantAppearance_WhenHandlerConfiguresAppearance_ReturnsStoredValue()
    {
        var context = new ChatResponseHandlerContext
        {
            Prompt = "Help me",
            ConnectionId = "connection-1",
            SessionId = "session-1",
            ChatType = ChatContextType.AIChatSession,
            ConversationHistory = [],
            Services = new ServiceCollection().BuildServiceProvider(),
        };

        context.AssistantAppearance = new AssistantMessageAppearance
        {
            Icon = "fa-solid fa-headset",
            CssClass = "text-secondary",
            DisableStreamingAnimation = true,
        };

        var result = context.AssistantAppearance;

        Assert.NotNull(result);
        Assert.Equal("fa-solid fa-headset", result.Icon);
        Assert.Equal("text-secondary", result.CssClass);
        Assert.True(result.DisableStreamingAnimation);
    }
}

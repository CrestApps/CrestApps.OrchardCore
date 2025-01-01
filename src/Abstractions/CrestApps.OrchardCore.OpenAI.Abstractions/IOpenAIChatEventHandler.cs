namespace CrestApps.OrchardCore.OpenAI.Models;

public interface IOpenAIChatEventHandler
{
    Task CompletedAsync(OpenAICompletedChatContext context);
}

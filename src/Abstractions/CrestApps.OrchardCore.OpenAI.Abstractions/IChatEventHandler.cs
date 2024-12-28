namespace CrestApps.OrchardCore.OpenAI.Models;

public interface IChatEventHandler
{
    Task CompletedAsync(CompletedChatContext context);
}

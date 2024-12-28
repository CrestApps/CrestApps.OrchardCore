using CrestApps.OrchardCore.OpenAI.Models;

namespace CrestApps.OrchardCore.OpenAI;

public interface IChatCompletionService
{
    string Name { get; }

    Task<ChatCompletionResponse> ChatAsync(IEnumerable<ChatCompletionMessage> messages, ChatCompletionContext context);
}

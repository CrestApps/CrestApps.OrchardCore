using CrestApps.OrchardCore.OpenAI.Models;

namespace CrestApps.OrchardCore.OpenAI;

public interface IOpenAIChatCompletionService
{
    string Name { get; }

    Task<OpenAIChatCompletionResponse> ChatAsync(IEnumerable<OpenAIChatCompletionMessage> messages, OpenAIChatCompletionContext context);
}

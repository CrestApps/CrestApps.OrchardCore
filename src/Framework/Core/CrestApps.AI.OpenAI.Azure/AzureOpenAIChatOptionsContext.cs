using CrestApps.AI.Models;
using OpenAI.Chat;

namespace CrestApps.AI.OpenAI.Azure;

public sealed class AzureOpenAIChatOptionsContext
{
    public AzureOpenAIChatOptionsContext(
        ChatCompletionOptions chatCompletionOptions,
        AICompletionContext completionContext,
        List<ChatMessage> prompts)
    {
        ArgumentNullException.ThrowIfNull(chatCompletionOptions);
        ArgumentNullException.ThrowIfNull(completionContext);
        ArgumentNullException.ThrowIfNull(prompts);

        ChatCompletionOptions = chatCompletionOptions;
        CompletionContext = completionContext;
        Prompts = prompts;
    }

    public ChatCompletionOptions ChatCompletionOptions { get; }

    public AICompletionContext CompletionContext { get; }

    public List<ChatMessage> Prompts { get; }

    public List<Microsoft.Extensions.AI.AIFunction> SystemFunctions { get; } = [];
}

using CrestApps.OrchardCore.AI.Models;
using OpenAI.Chat;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core;

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
}

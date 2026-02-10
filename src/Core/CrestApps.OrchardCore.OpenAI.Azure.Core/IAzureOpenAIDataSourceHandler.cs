using OpenAI.Chat;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core;

public interface IAzureOpenAIDataSourceHandler
{
    ValueTask ConfigureSourceAsync(ChatCompletionOptions options, AzureOpenAIDataSourceContext context);

    ValueTask ConfigureOptionsAsync(AzureOpenAIChatOptionsContext context)
        => ValueTask.CompletedTask;
}

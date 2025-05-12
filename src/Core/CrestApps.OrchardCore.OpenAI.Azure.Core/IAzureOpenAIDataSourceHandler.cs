using OpenAI.Chat;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core;

public interface IAzureOpenAIDataSourceHandler
{
    bool CanHandle(string type);

    ValueTask ConfigureSourceAsync(ChatCompletionOptions options, AzureOpenAIDataSourceContext context);
}

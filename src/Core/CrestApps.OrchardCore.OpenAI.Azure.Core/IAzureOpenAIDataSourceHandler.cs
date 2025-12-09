using OpenAI.Chat;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core;

[Obsolete("This interface should not be used any more. Instead use IAICompletionServiceHandler")]
public interface IAzureOpenAIDataSourceHandler
{
    bool CanHandle(string type);

    ValueTask ConfigureSourceAsync(ChatCompletionOptions options, AzureOpenAIDataSourceContext context);
}

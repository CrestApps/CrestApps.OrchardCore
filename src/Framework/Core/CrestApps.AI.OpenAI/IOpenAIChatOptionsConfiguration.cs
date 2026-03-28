using CrestApps.AI.Models;
using OpenAI.Chat;

namespace CrestApps.AI.OpenAI;

public interface IOpenAIChatOptionsConfiguration
{
    Task InitializeConfigurationAsync(CompletionServiceConfigureContext context);

    void Configure(CompletionServiceConfigureContext context, ChatCompletionOptions chatCompletionOptions);
}

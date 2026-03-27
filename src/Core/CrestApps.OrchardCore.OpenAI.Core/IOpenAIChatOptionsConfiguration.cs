using CrestApps.OrchardCore.AI.Models;
using OpenAI.Chat;

namespace CrestApps.OrchardCore.OpenAI.Core;

public interface IOpenAIChatOptionsConfiguration
{
    Task InitializeConfigurationAsync(CompletionServiceConfigureContext context);

    void Configure(CompletionServiceConfigureContext context, ChatCompletionOptions chatCompletionOptions);
}

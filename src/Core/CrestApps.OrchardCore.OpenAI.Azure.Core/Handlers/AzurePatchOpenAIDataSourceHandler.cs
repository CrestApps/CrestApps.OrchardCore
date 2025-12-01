using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Core;
using OpenAI.Chat;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Handlers;

public sealed class AzurePatchOpenAIDataSourceHandler : IOpenAIChatOptionsConfiguration
{
    public void Configure(CompletionServiceConfigureContext context, ChatCompletionOptions chatCompletionOptions)
    {
#pragma warning disable SCME0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        if (context.ChatOptions.MaxOutputTokens.HasValue)
        {
            // This is a workaround until we have first-class support for MaxTokens in ChatCompletionOptions in Azure.
            chatCompletionOptions.Patch.Set("$.max_tokens"u8, context.ChatOptions.MaxOutputTokens.Value);

            context.ChatOptions.MaxOutputTokens = null;
        }

        if (context.IsStreaming)
        {
            chatCompletionOptions.Patch.Set("$.stream"u8, true);
            chatCompletionOptions.Patch.Remove("$.stream_options"u8);
        }
#pragma warning restore SCME0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    }

    public Task InitializeConfigurationAsync(CompletionServiceConfigureContext configureContext)
    {
        return Task.CompletedTask;
    }
}

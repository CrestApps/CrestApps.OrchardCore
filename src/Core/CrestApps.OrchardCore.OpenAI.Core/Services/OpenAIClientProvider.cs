using System.ClientModel;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;
using OpenAI;

namespace CrestApps.OrchardCore.OpenAI.Core.Services;

public sealed class OpenAIClientProvider : AIClientProviderBase
{
    public OpenAIClientProvider(IServiceProvider serviceProvider)
        : base(serviceProvider)
    {
    }

    protected override string GetProviderName()
        => OpenAIConstants.ProviderName;

    protected override IChatClient GetChatClient(AIProviderConnectionEntry connection, string deploymentName)
    {
        var client = GetOpenAIClient(connection);

        return client
             .GetChatClient(deploymentName)
             .AsIChatClient();
    }

    protected override IEmbeddingGenerator<string, Embedding<float>> GetEmbeddingGenerator(AIProviderConnectionEntry connection, string deploymentName)
    {
        var client = GetOpenAIClient(connection);

        return client.GetEmbeddingClient(deploymentName)
            .AsIEmbeddingGenerator();
    }

#pragma warning disable MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    protected override ISpeechToTextClient GetSpeechToTextClient(AIProviderConnectionEntry connection, string deploymentName)
#pragma warning restore MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    {
        var client = GetOpenAIClient(connection);

#pragma warning disable MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        return client.GetAudioClient(deploymentName)
            .AsISpeechToTextClient();
    }

    protected override IImageGenerator GetImageGenerator(AIProviderConnectionEntry connection, string deploymentName)
#pragma warning restore MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    {
        var client = GetOpenAIClient(connection);

#pragma warning disable MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        return client.GetImageClient(deploymentName)
            .AsIImageGenerator();
#pragma warning restore MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    }

    private static OpenAIClient GetOpenAIClient(AIProviderConnectionEntry connection)
    {
        var endpoint = connection.GetEndpoint(false);

        if (endpoint is null)
        {
            return new OpenAIClient(connection.GetApiKey());
        }

        return new OpenAIClient(new ApiKeyCredential(connection.GetApiKey()), new OpenAIClientOptions
        {
            Endpoint = endpoint,
        });
    }
}

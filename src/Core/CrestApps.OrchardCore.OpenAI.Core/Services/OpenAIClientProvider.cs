using System.ClientModel;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;
using OpenAI;

namespace CrestApps.OrchardCore.OpenAI.Core.Services;

public sealed class OpenAIClientProvider : AIClientProviderBase
{
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

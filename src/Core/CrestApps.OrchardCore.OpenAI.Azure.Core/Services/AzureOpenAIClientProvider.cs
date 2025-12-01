using System.ClientModel;
using System.ClientModel.Primitives;
using Azure.Identity;
using CrestApps.Azure.Core;
using CrestApps.Azure.Core.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Services;

public sealed class AzureOpenAIClientProvider : AIClientProviderBase
{
    private readonly ILoggerFactory _loggerFactory;

    protected override string GetProviderName()
        => AzureOpenAIConstants.ProviderName;

    public AzureOpenAIClientProvider(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    protected override IChatClient GetChatClient(AIProviderConnectionEntry connection, string deploymentName)
    {
        var endpoint = new Uri($"{connection.GetEndpoint()}openai/deployments/{deploymentName}?api-version=2025-01-01-preview");

        return GetClient(connection, endpoint)
            .GetChatClient(deploymentName)
            .AsIChatClient();
    }

    protected override IEmbeddingGenerator<string, Embedding<float>> GetEmbeddingGenerator(AIProviderConnectionEntry connection, string deploymentName)
    {
        var endpoint = connection.GetEndpoint();

        return GetClient(connection, endpoint)
            .GetEmbeddingClient(deploymentName)
            .AsIEmbeddingGenerator();
    }

    private OpenAIClient GetClient(AIProviderConnectionEntry connection, Uri endpoint)
    {
        var options = new OpenAIClientOptions
        {
            Endpoint = endpoint,
            ClientLoggingOptions = new ClientLoggingOptions
            {
                EnableLogging = true,
                EnableMessageLogging = true,
                EnableMessageContentLogging = true,
                LoggerFactory = _loggerFactory,
            },
        };

        return connection.GetAzureAuthenticationType() switch
        {
            AzureAuthenticationType.ApiKey => new OpenAIClient(new ApiKeyCredential(connection.GetApiKey()), options),

#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            AzureAuthenticationType.ManagedIdentity => new OpenAIClient(new BearerTokenPolicy(new ManagedIdentityCredential(), "https://ai.azure.com/.default"), options),
            AzureAuthenticationType.Default => new OpenAIClient(new BearerTokenPolicy(new DefaultAzureCredential(), "https://ai.azure.com/.default"), options),
            _ => throw new NotSupportedException("The provided authentication type is not supported.")
        };
#pragma warning restore OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    }
}

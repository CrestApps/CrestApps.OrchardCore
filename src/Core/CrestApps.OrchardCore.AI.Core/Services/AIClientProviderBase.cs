using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.Core.Services;

public abstract class AIClientProviderBase : IAIClientProvider
{
    public bool CanHandle(string providerName)
        => string.Equals(GetProviderName(), providerName, StringComparison.OrdinalIgnoreCase);

    public ValueTask<IChatClient> GetChatClientAsync(AIProviderConnectionEntry connection, string deploymentName = null)
    {
        if (string.IsNullOrEmpty(deploymentName))
        {
            deploymentName = connection.GetDefaultDeploymentName(false);
        }

        if (string.IsNullOrEmpty(deploymentName))
        {
            throw new ArgumentException("A deployment name must be provided, either directly or as a default in the connection settings.");
        }

        return ValueTask.FromResult(GetChatClient(connection, deploymentName));
    }

    public ValueTask<IEmbeddingGenerator<string, Embedding<float>>> GetEmbeddingGeneratorAsync(AIProviderConnectionEntry connection, string deploymentName = null)
    {
        if (string.IsNullOrEmpty(deploymentName))
        {
            deploymentName = connection.GetDefaultEmbeddingDeploymentName(false);
        }

        if (string.IsNullOrEmpty(deploymentName))
        {
            throw new ArgumentException("An embedding deployment name must be provided, either directly or as a default in the connection settings.");
        }

        return ValueTask.FromResult(GetEmbeddingGenerator(connection, deploymentName));
    }

#pragma warning disable MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    public ValueTask<ISpeechToTextClient> GetSpeechToTextClientAsync(AIProviderConnectionEntry connection, string deploymentName = null)
#pragma warning restore MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    {
        if (string.IsNullOrEmpty(deploymentName))
        {
            deploymentName = connection.GetDefaultSpeechToTextDeploymentName(false);
        }

        if (string.IsNullOrEmpty(deploymentName))
        {
            throw new ArgumentException("A Speech-to-text deployment name must be provided, either directly or as a default in the connection settings.");
        }

        return ValueTask.FromResult(GetSpeechToTextClient(connection, deploymentName));
    }

    protected abstract string GetProviderName();

    protected abstract IChatClient GetChatClient(AIProviderConnectionEntry connection, string deploymentName);

    protected abstract IEmbeddingGenerator<string, Embedding<float>> GetEmbeddingGenerator(AIProviderConnectionEntry connection, string deploymentName);

#pragma warning disable MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    protected abstract ISpeechToTextClient GetSpeechToTextClient(AIProviderConnectionEntry connection, string deploymentName);
#pragma warning restore MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
}

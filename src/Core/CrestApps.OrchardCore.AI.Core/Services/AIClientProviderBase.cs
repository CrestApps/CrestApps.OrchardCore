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

    protected abstract string GetProviderName();

    protected abstract IChatClient GetChatClient(AIProviderConnectionEntry connection, string deploymentName);

    protected abstract IEmbeddingGenerator<string, Embedding<float>> GetEmbeddingGenerator(AIProviderConnectionEntry connection, string deploymentName);
}

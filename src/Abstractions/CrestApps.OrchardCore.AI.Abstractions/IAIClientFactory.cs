using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Defines a factory for creating AI clients, such as chat clients and embedding generators,
/// based on the specified provider, connection, and deployment names.
/// </summary>
public interface IAIClientFactory
{
    /// <summary>
    /// Asynchronously creates an <see cref="IChatClient"/> instance for the given provider, connection, and deployment.
    /// </summary>
    /// <param name="providerName">The name of the AI provider (e.g., "OpenAI", "AzureOpenAI").</param>
    /// <param name="connectionName">The name of the connection configuration to use.</param>
    /// <param name="deploymentName">The name of the deployment or model to use.</param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation, with the created <see cref="IChatClient"/>.
    /// </returns>
    ValueTask<IChatClient> CreateChatClientAsync(string providerName, string connectionName, string deploymentName);

    /// <summary>
    /// Asynchronously creates an <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> instance for the given provider, connection, and deployment.
    /// </summary>
    /// <param name="providerName">The name of the AI provider (e.g., "OpenAI", "AzureOpenAI").</param>
    /// <param name="connectionName">The name of the connection configuration to use.</param>
    /// <param name="deploymentName">The name of the deployment or model to use.</param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation, with the created <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/>.
    /// </returns>
    ValueTask<IEmbeddingGenerator<string, Embedding<float>>> CreateEmbeddingGeneratorAsync(string providerName, string connectionName, string deploymentName);
}

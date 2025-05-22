using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Provides methods to obtain AI chat clients and embedding generators for specific providers.
/// </summary>
public interface IAIClientProvider
{
    /// <summary>
    /// Determines whether this provider can handle the specified provider name.
    /// </summary>
    /// <param name="providerName">The name of the provider to check.</param>
    /// <returns><c>true</c> if the provider can be handled; otherwise, <c>false</c>.</returns>
    bool CanHandle(string providerName);

    /// <summary>
    /// Gets an AI chat client for the specified connection and deployment.
    /// </summary>
    /// <param name="connection">The connection entry containing provider configuration.</param>
    /// <param name="deploymentName">The optional deployment name to use.</param>
    /// <returns>A <see cref="ValueTask{IChatClient}"/> representing the asynchronous operation.</returns>
    ValueTask<IChatClient> GetChatClientAsync(AIProviderConnectionEntry connection, string deploymentName = null);

    /// <summary>
    /// Gets an embedding generator for the specified connection and deployment.
    /// </summary>
    /// <param name="connection">The connection entry containing provider configuration.</param>
    /// <param name="deploymentName">The optional deployment name to use.</param>
    /// <returns>A <see cref="ValueTask{IEmbeddingGenerator}"/> representing the asynchronous operation.</returns>
    ValueTask<IEmbeddingGenerator<string, Embedding<float>>> GetEmbeddingGeneratorAsync(AIProviderConnectionEntry connection, string deploymentName = null);
}

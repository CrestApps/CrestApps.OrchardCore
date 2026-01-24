using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultAIClientFactory : IAIClientFactory
{
    private readonly AIProviderOptions _options;
    private readonly IEnumerable<IAIClientProvider> _clientProviders;

    public DefaultAIClientFactory(
        IEnumerable<IAIClientProvider> clientProviders,
        IOptions<AIProviderOptions> options)
    {
        _options = options.Value;
        _clientProviders = clientProviders;
    }

    public ValueTask<IChatClient> CreateChatClientAsync(string providerName, string connectionName, string deploymentName = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerName);
        ArgumentException.ThrowIfNullOrEmpty(connectionName);

        if (!_options.Providers.TryGetValue(providerName, out var provider))
        {
            throw new ArgumentException($"Provider '{providerName}' not found.");
        }

        if (!provider.Connections.TryGetValue(connectionName, out var connection))
        {
            throw new ArgumentException($"Connection '{connectionName}' not found with in the provider '{providerName}'.");
        }

        foreach (var clientProvider in _clientProviders)
        {
            if (!clientProvider.CanHandle(providerName))
            {
                continue;
            }

            return clientProvider.GetChatClientAsync(connection, deploymentName);
        }

        throw new ArgumentException($"Unable to find an implementation of '{nameof(IAIClientProvider)}' that can handle the provider '{providerName}'.");
    }

    public ValueTask<IEmbeddingGenerator<string, Embedding<float>>> CreateEmbeddingGeneratorAsync(string providerName, string connectionName, string deploymentName = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerName);
        ArgumentException.ThrowIfNullOrEmpty(connectionName);

        if (!_options.Providers.TryGetValue(providerName, out var provider))
        {
            throw new ArgumentException($"Provider '{providerName}' not found.");
        }

        if (!provider.Connections.TryGetValue(connectionName, out var connection))
        {
            throw new ArgumentException($"Connection '{connectionName}' not found with in the provider '{providerName}'.");
        }

        foreach (var clientProvider in _clientProviders)
        {
            if (!clientProvider.CanHandle(providerName))
            {
                continue;
            }

            return clientProvider.GetEmbeddingGeneratorAsync(connection, deploymentName);
        }

        throw new ArgumentException($"Unable to find an implementation of '{nameof(IAIClientProvider)}' that can handle the provider '{providerName}'.");
    }

#pragma warning disable MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    public ValueTask<IImageGenerator> CreateImageGeneratorAsync(string providerName, string connectionName, string deploymentName = null)
#pragma warning restore MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    {
        ArgumentException.ThrowIfNullOrEmpty(providerName);
        ArgumentException.ThrowIfNullOrEmpty(connectionName);

        if (!_options.Providers.TryGetValue(providerName, out var provider))
        {
            throw new ArgumentException($"Provider '{providerName}' not found.");
        }

        if (!provider.Connections.TryGetValue(connectionName, out var connection))
        {
            throw new ArgumentException($"Connection '{connectionName}' not found with in the provider '{providerName}'.");
        }

        foreach (var clientProvider in _clientProviders)
        {
            if (!clientProvider.CanHandle(providerName))
            {
                continue;
            }

            return clientProvider.GetImageGeneratorAsync(connection, deploymentName);
        }

        throw new ArgumentException($"Unable to find an implementation of '{nameof(IAIClientProvider)}' that can handle the provider '{providerName}'.");
    }
}

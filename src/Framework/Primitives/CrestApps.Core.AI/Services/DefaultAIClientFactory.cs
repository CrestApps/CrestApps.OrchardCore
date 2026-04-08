using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.Core.AI.Services;

public sealed class DefaultAIClientFactory : IAIClientFactory
{
    private readonly AIProviderOptions _options;
    private readonly IEnumerable<IAIClientProvider> _clientProviders;

    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DefaultAIClientFactory> _logger;

    public DefaultAIClientFactory(
        IEnumerable<IAIClientProvider> clientProviders,
        IDataProtectionProvider dataProtectionProvider,
        IServiceProvider serviceProvider,
        ILogger<DefaultAIClientFactory> logger,
        IOptions<AIProviderOptions> options)
    {
        _options = options.Value;
        _clientProviders = clientProviders;
        _dataProtectionProvider = dataProtectionProvider;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async ValueTask<IChatClient> CreateChatClientAsync(string providerName, string connectionName, string deploymentName = null)
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

            var client = await clientProvider.GetChatClientAsync(connection, deploymentName);

            return new AICompletionUsageTrackingChatClient(
                client,
                providerName,
                providerName,
                connectionName,
                deploymentName,
                _serviceProvider,
                _logger);

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

#pragma warning disable MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    public ValueTask<ISpeechToTextClient> CreateSpeechToTextClientAsync(string providerName, string connectionName, string deploymentName = null)
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

            return clientProvider.GetSpeechToTextClientAsync(connection, deploymentName);

        }

        throw new ArgumentException($"Unable to find an implementation of '{nameof(IAIClientProvider)}' that can handle the provider '{providerName}'.");

    }

#pragma warning disable MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    public ValueTask<ISpeechToTextClient> CreateSpeechToTextClientAsync(AIDeployment deployment)
#pragma warning restore MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    {
        ArgumentNullException.ThrowIfNull(deployment);

        ArgumentException.ThrowIfNullOrEmpty(deployment.ClientName);

        // When the deployment has a connection reference, use the standard path.
        if (!string.IsNullOrEmpty(deployment.ConnectionName))
        {
            return CreateSpeechToTextClientAsync(deployment.ClientName, deployment.ConnectionName, deployment.ModelName);

        }

        // Contained-connection deployment: build an AIProviderConnectionEntry from the deployment's Properties.

        var connectionEntry = AIDeploymentConnectionEntryFactory.Create(deployment, _dataProtectionProvider);

        foreach (var clientProvider in _clientProviders)
        {
            if (!clientProvider.CanHandle(deployment.ClientName))
            {
                continue;

            }

            return clientProvider.GetSpeechToTextClientAsync(connectionEntry, deployment.ModelName);

        }

        throw new ArgumentException($"Unable to find an implementation of '{nameof(IAIClientProvider)}' that can handle the provider '{deployment.ClientName}'.");

    }

#pragma warning disable MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    public ValueTask<ITextToSpeechClient> CreateTextToSpeechClientAsync(string providerName, string connectionName, string deploymentName = null)
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

            return clientProvider.GetTextToSpeechClientAsync(connection, deploymentName);

        }

        throw new ArgumentException($"Unable to find an implementation of '{nameof(IAIClientProvider)}' that can handle the provider '{providerName}'.");

    }

    public ValueTask<ITextToSpeechClient> CreateTextToSpeechClientAsync(AIDeployment deployment)
    {
        ArgumentNullException.ThrowIfNull(deployment);

        ArgumentException.ThrowIfNullOrEmpty(deployment.ClientName);

        // When the deployment has a connection reference, use the standard path.
        if (!string.IsNullOrEmpty(deployment.ConnectionName))
        {
            return CreateTextToSpeechClientAsync(deployment.ClientName, deployment.ConnectionName, deployment.ModelName);

        }

        // Contained-connection deployment: build an AIProviderConnectionEntry from the deployment's Properties.

        var connectionEntry = AIDeploymentConnectionEntryFactory.Create(deployment, _dataProtectionProvider);

        foreach (var clientProvider in _clientProviders)
        {
            if (!clientProvider.CanHandle(deployment.ClientName))
            {
                continue;

            }

            return clientProvider.GetTextToSpeechClientAsync(connectionEntry, deployment.ModelName);

        }

        throw new ArgumentException($"Unable to find an implementation of '{nameof(IAIClientProvider)}' that can handle the provider '{deployment.ClientName}'.");
    }

#pragma warning restore MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
}

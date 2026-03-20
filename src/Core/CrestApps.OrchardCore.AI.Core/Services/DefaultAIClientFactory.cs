using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultAIClientFactory : IAIClientFactory
{
    private readonly AIProviderOptions _options;
    private readonly IEnumerable<IAIClientProvider> _clientProviders;
    private readonly IDataProtectionProvider _dataProtectionProvider;

    public DefaultAIClientFactory(
        IEnumerable<IAIClientProvider> clientProviders,
        IDataProtectionProvider dataProtectionProvider,
        IOptions<AIProviderOptions> options)
    {
        _options = options.Value;
        _clientProviders = clientProviders;
        _dataProtectionProvider = dataProtectionProvider;
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
        ArgumentException.ThrowIfNullOrEmpty(deployment.ProviderName);

        // When the deployment has a connection reference, use the standard path.
        if (!string.IsNullOrEmpty(deployment.ConnectionName))
        {
            return CreateSpeechToTextClientAsync(deployment.ProviderName, deployment.ConnectionName, deployment.Name);
        }

        // Contained-connection deployment: build an AIProviderConnectionEntry from the deployment's Properties.
        var connectionEntry = BuildConnectionEntry(deployment);

        foreach (var clientProvider in _clientProviders)
        {
            if (!clientProvider.CanHandle(deployment.ProviderName))
            {
                continue;
            }

            return clientProvider.GetSpeechToTextClientAsync(connectionEntry, deployment.Name);
        }

        throw new ArgumentException($"Unable to find an implementation of '{nameof(IAIClientProvider)}' that can handle the provider '{deployment.ProviderName}'.");
    }

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
        ArgumentException.ThrowIfNullOrEmpty(deployment.ProviderName);

        // When the deployment has a connection reference, use the standard path.
        if (!string.IsNullOrEmpty(deployment.ConnectionName))
        {
            return CreateTextToSpeechClientAsync(deployment.ProviderName, deployment.ConnectionName, deployment.Name);
        }

        // Contained-connection deployment: build an AIProviderConnectionEntry from the deployment's Properties.
        var connectionEntry = BuildConnectionEntry(deployment);

        foreach (var clientProvider in _clientProviders)
        {
            if (!clientProvider.CanHandle(deployment.ProviderName))
            {
                continue;
            }

            return clientProvider.GetTextToSpeechClientAsync(connectionEntry, deployment.Name);
        }

        throw new ArgumentException($"Unable to find an implementation of '{nameof(IAIClientProvider)}' that can handle the provider '{deployment.ProviderName}'.");
    }

    public async Task<SpeechVoice[]> GetSpeechVoicesAsync(AIDeployment deployment)
    {
        ArgumentNullException.ThrowIfNull(deployment);
        ArgumentException.ThrowIfNullOrEmpty(deployment.ProviderName);

        var connectionEntry = GetConnectionEntry(deployment);

        foreach (var clientProvider in _clientProviders)
        {
            if (!clientProvider.CanHandle(deployment.ProviderName))
            {
                continue;
            }

            return await clientProvider.GetSpeechVoicesAsync(connectionEntry, deployment.Name);
        }

        return [];
    }

    private AIProviderConnectionEntry GetConnectionEntry(AIDeployment deployment)
    {
        if (!string.IsNullOrEmpty(deployment.ConnectionName))
        {
            if (_options.Providers.TryGetValue(deployment.ProviderName, out var provider)
                && provider.Connections.TryGetValue(deployment.ConnectionName, out var connection))
            {
                return connection;
            }

            throw new ArgumentException($"Connection '{deployment.ConnectionName}' not found within the provider '{deployment.ProviderName}'.");
        }

        return BuildConnectionEntry(deployment);
    }

    private AIProviderConnectionEntry BuildConnectionEntry(AIDeployment deployment)
    {
        var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        if (deployment.Properties != null)
        {
            foreach (var property in deployment.Properties)
            {
                values[property.Key] = ConvertJsonNode(property.Value);
            }
        }

        UnprotectApiKeys(values);

        return new AIProviderConnectionEntry(values);
    }

    private object ConvertJsonNode(JsonNode node)
    {
        return node switch
        {
            JsonObject jsonObject => jsonObject.ToDictionary(
                property => property.Key,
                property => ConvertJsonNode(property.Value),
                StringComparer.OrdinalIgnoreCase),
            JsonArray jsonArray => jsonArray.Select(ConvertJsonNode).ToList(),
            JsonValue jsonValue when jsonValue.TryGetValue<string>(out var s) => s,
            JsonValue jsonValue when jsonValue.TryGetValue<bool>(out var b) => b,
            JsonValue jsonValue when jsonValue.TryGetValue<int>(out var i) => i,
            JsonValue jsonValue when jsonValue.TryGetValue<long>(out var l) => l,
            JsonValue jsonValue when jsonValue.TryGetValue<double>(out var d) => d,
            _ => node?.ToString(),
        };
    }

    private void UnprotectApiKeys(IDictionary<string, object> values)
    {
        foreach (var (key, value) in values.ToList())
        {
            switch (value)
            {
                case IDictionary<string, object> nestedDictionary:
                    UnprotectApiKeys(nestedDictionary);
                    break;

                case List<object> items:
                    UnprotectApiKeys(items);
                    break;

                case string encryptedKey when
                    string.Equals(key, "ApiKey", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(encryptedKey):
                {
                    var protector = _dataProtectionProvider.CreateProtector(AIConstants.ConnectionProtectorName);
                    values[key] = protector.Unprotect(encryptedKey);
                    break;
                }
            }
        }
    }

    private void UnprotectApiKeys(List<object> values)
    {
        foreach (var value in values)
        {
            switch (value)
            {
                case IDictionary<string, object> nestedDictionary:
                    UnprotectApiKeys(nestedDictionary);
                    break;

                case List<object> nestedList:
                    UnprotectApiKeys(nestedList);
                    break;
            }
        }
    }
}

using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultSpeechVoiceResolver : ISpeechVoiceResolver
{
    private readonly IEnumerable<IAIClientProvider> _clientProviders;
    private readonly AIProviderOptions _options;
    private readonly IDataProtectionProvider _dataProtectionProvider;

    public DefaultSpeechVoiceResolver(
        IEnumerable<IAIClientProvider> clientProviders,
        IDataProtectionProvider dataProtectionProvider,
        IOptions<AIProviderOptions> options)
    {
        _clientProviders = clientProviders;
        _dataProtectionProvider = dataProtectionProvider;
        _options = options.Value;
    }

    public async Task<SpeechVoice[]> GetSpeechVoicesAsync(AIDeployment deployment)
    {
        ArgumentNullException.ThrowIfNull(deployment);
        ArgumentException.ThrowIfNullOrEmpty(deployment.ClientName);

        var connectionEntry = GetConnectionEntry(deployment);

        foreach (var clientProvider in _clientProviders)
        {
            if (!clientProvider.CanHandle(deployment.ClientName))
            {
                continue;
            }

            return await clientProvider.GetSpeechVoicesAsync(connectionEntry, deployment.ModelName);
        }

        return [];
    }

    private AIProviderConnectionEntry GetConnectionEntry(AIDeployment deployment)
    {
        if (!string.IsNullOrEmpty(deployment.ConnectionName))
        {
            if (_options.Providers.TryGetValue(deployment.ClientName, out var provider)
                && provider.Connections.TryGetValue(deployment.ConnectionName, out var connection))
            {
                return connection;
            }

            throw new InvalidOperationException(
                $"Unable to find connection '{deployment.ConnectionName}' for provider '{deployment.ClientName}'.");
        }

        return AIDeploymentConnectionEntryFactory.Create(deployment, _dataProtectionProvider);
    }
}

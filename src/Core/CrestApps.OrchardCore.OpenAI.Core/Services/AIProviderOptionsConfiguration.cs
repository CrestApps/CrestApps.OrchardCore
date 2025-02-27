using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Core.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.OpenAI.Core.Services;

public sealed class AIProviderOptionsConfiguration : IConfigureOptions<AIProviderOptions>
{
    private readonly IDocumentManager<OpenAIConnectionDocument> _documentManager;
    private readonly IDataProtectionProvider _dataProtectionProvider;

    public AIProviderOptionsConfiguration(
        IDocumentManager<OpenAIConnectionDocument> documentManager,
        IDataProtectionProvider dataProtectionProvider)
    {
        _documentManager = documentManager;
        _dataProtectionProvider = dataProtectionProvider;
    }

    public void Configure(AIProviderOptions options)
    {
        var document = _documentManager.GetOrCreateImmutableAsync()
            .GetAwaiter()
            .GetResult();

        if (document.Connections.Count == 0)
        {
            return;
        }

        if (!options.Providers.TryGetValue(OpenAIConstants.OpenAISettingsProviderName, out var provider))
        {
            provider = new AIProvider()
            {
                Connections = new Dictionary<string, AIProviderConnectionEntry>(),
            };
        }

        var protector = _dataProtectionProvider.CreateProtector(OpenAIConstants.ConnectionProtectorName);

        foreach (var connection in document.Connections.Values)
        {
            var values = new Dictionary<string, object>()
            {
                { "ApiKey", protector.Unprotect(connection.ApiKey) },
                { "Endpoint", connection.Endpoint },
                { "DefaultDeploymentName", connection.DefaultDeploymentName },
                { "ConnectionNameAlias", connection.Name },
            };

            provider.Connections[connection.Id] = new AIProviderConnectionEntry(values);
        }

        var defaultConnection = document.DefaultConnectionId is not null &&
            document.Connections.TryGetValue(document.DefaultConnectionId, out var defaultConn)
            ? defaultConn
            : document.Connections.First().Value;

        provider.DefaultConnectionName = defaultConnection.Id;
        provider.DefaultDeploymentName = defaultConnection.DefaultDeploymentName;

        options.Providers[OpenAIConstants.OpenAISettingsProviderName] = provider;
    }
}

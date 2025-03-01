using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Documents;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.OpenAI.Core.Services;

public sealed class AIProviderConnectionsOptionsConfiguration : IConfigureOptions<AIProviderOptions>
{
    private readonly IDocumentManager<ModelDocument<AIProviderConnection>> _documentManager;
    private readonly IEnumerable<IAIProviderConnectionHandler> _handlers;

    private readonly ILogger _logger;

    public AIProviderConnectionsOptionsConfiguration(
        IDocumentManager<ModelDocument<AIProviderConnection>> documentManager,
        IEnumerable<IAIProviderConnectionHandler> handlers,
        ILogger<AIProviderConnectionsOptionsConfiguration> logger)
    {
        _documentManager = documentManager;
        _handlers = handlers;
        _logger = logger;
    }

    public void Configure(AIProviderOptions options)
    {
        var document = _documentManager.GetOrCreateMutableAsync()
            .GetAwaiter()
            .GetResult();

        if (document.Records.Count == 0)
        {
            return;
        }

        var groups = document.Records.Values
            .GroupBy(x => x.ProviderName)
            .Select(x => new
            {
                ProviderName = x.Key,
                Connections = x,
            }).ToList();

        foreach (var group in groups)
        {
            if (!options.Providers.TryGetValue(group.ProviderName, out var provider))
            {
                provider = new AIProvider()
                {
                    Connections = new Dictionary<string, AIProviderConnectionEntry>(),
                };
            }
            AIProviderConnection defaultConnection = null;

            foreach (var connection in group.Connections)
            {
                var mappingContext = new MappingAIProviderConnectionContext(connection);

                if (defaultConnection is null && connection.IsDefault)
                {
                    defaultConnection = connection;
                }

                mappingContext.Values["DefaultDeploymentName"] = connection.DefaultDeploymentName;
                mappingContext.Values["ConnectionNameAlias"] = connection.Name;

                _handlers.Invoke((handler, ctx) => handler.Mapping(ctx), mappingContext, _logger);

                provider.Connections[connection.Id] = new AIProviderConnectionEntry(mappingContext.Values);
            }

            if (defaultConnection is not null)
            {
                provider.DefaultConnectionName = defaultConnection.Id;
                provider.DefaultDeploymentName = defaultConnection.DefaultDeploymentName;
            }

            options.Providers[group.ProviderName] = provider;
        }
    }
}

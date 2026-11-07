using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Deployments.Steps;
using CrestApps.OrchardCore.Core.Deployments;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Deployments.Sources;

/// <summary>
/// Writes the selected AI provider connections to a deployment plan. The connection handlers strip the secrets while
/// each connection is exported.
/// </summary>
internal sealed class AIProviderConnectionDeploymentSource : CatalogDeploymentSourceBase<AIProviderConnectionDeploymentStep, AIProviderConnection>
{
    private readonly INamedCatalog<AIProviderConnection> _connectionsCatalog;
    private readonly IEnumerable<IAIProviderConnectionHandler> _handlers;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProviderConnectionDeploymentSource"/> class.
    /// </summary>
    /// <param name="connectionsCatalog">The catalog for retrieving AI provider connections by name.</param>
    /// <param name="handlers">The collection of handlers invoked during connection export.</param>
    /// <param name="logger">The logger instance for this source.</param>
    public AIProviderConnectionDeploymentSource(
        INamedCatalog<AIProviderConnection> connectionsCatalog,
        IEnumerable<IAIProviderConnectionHandler> handlers,
        ILogger<AIProviderConnectionDeploymentSource> logger)
        : base("connections")
    {
        _connectionsCatalog = connectionsCatalog;
        _handlers = handlers;
        _logger = logger;
    }

    protected override async Task<IEnumerable<AIProviderConnection>> GetEntriesAsync()
        => await _connectionsCatalog.GetAllAsync();

    protected override bool IsSelected(AIProviderConnectionDeploymentStep step, AIProviderConnection entry)
        => IsSelectedByKey(step.IncludeAll, step.ConnectionIds, entry.ItemId);

    protected override JsonObject Export(AIProviderConnection entry)
    {
        var connectionObject = new JsonObject()
        {
            { "ItemId", entry.ItemId },
            { "Source", entry.Source },
            { "Name", entry.Name },
            { "DisplayText", entry.DisplayText },
            { "CreatedUtc", entry.CreatedUtc },
            { "OwnerId", entry.OwnerId },
            { "Author", entry.Author },
            { "Properties", JsonSerializer.SerializeToNode(entry.Properties) },
        };

        var exportingContext = new ExportingAIProviderConnectionContext(entry, connectionObject);

        _handlers.Invoke((handler, context) => handler.Exporting(context), exportingContext, _logger);

        return connectionObject;
    }
}

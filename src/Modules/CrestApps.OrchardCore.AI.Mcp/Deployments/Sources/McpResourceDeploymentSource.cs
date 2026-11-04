using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Mcp;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Mcp.Deployments.Steps;
using CrestApps.OrchardCore.Core.Deployments;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Mcp.Deployments.Sources;

/// <summary>
/// Writes the selected MCP resources to a deployment plan. The resource handlers strip the secrets while each resource
/// is exported.
/// </summary>
internal sealed class McpResourceDeploymentSource : CatalogDeploymentSourceBase<McpResourceDeploymentStep, McpResource>
{
    private readonly ICatalog<McpResource> _store;
    private readonly IEnumerable<IMcpResourceHandler> _handlers;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpResourceDeploymentSource"/> class.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="handlers">The handlers.</param>
    /// <param name="logger">The logger.</param>
    public McpResourceDeploymentSource(
        ICatalog<McpResource> store,
        IEnumerable<IMcpResourceHandler> handlers,
        ILogger<McpResourceDeploymentSource> logger)
        : base("Resources")
    {
        _store = store;
        _handlers = handlers;
        _logger = logger;
    }

    protected override async Task<IEnumerable<McpResource>> GetEntriesAsync()
        => await _store.GetAllAsync();

    protected override bool IsSelected(McpResourceDeploymentStep step, McpResource entry)
        => IsSelectedByKey(step.IncludeAll, step.ResourceIds, entry.ItemId);

    protected override JsonObject Export(McpResource entry)
    {
        var resourceData = new JsonObject
        {
            { nameof(Resource.Uri), entry.Resource?.Uri },
            { nameof(Resource.Name), entry.Resource?.Name },
            { nameof(Resource.Title), entry.Resource?.Title },
            { nameof(Resource.Description), entry.Resource?.Description },
            { nameof(Resource.MimeType), entry.Resource?.MimeType },
        };

        var deploymentInfo = new JsonObject()
        {
            { nameof(McpResource.ItemId), entry.ItemId },
            { nameof(McpResource.Source), entry.Source },
            { nameof(McpResource.DisplayText), entry.DisplayText },
            { nameof(McpResource.Author), entry.Author },
            { nameof(McpResource.CreatedUtc), entry.CreatedUtc },
            { nameof(McpResource.OwnerId), entry.OwnerId },
            { nameof(McpResource.Resource), resourceData },
            { nameof(McpResource.Properties), JsonSerializer.SerializeToNode(entry.Properties) },
        };

        var exportingContext = new ExportingMcpResourceContext(entry, deploymentInfo);

        _handlers.Invoke((handler, context) => handler.Exporting(context), exportingContext, _logger);

        return deploymentInfo;
    }
}

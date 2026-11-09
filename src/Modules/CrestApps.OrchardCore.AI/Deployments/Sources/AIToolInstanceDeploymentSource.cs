using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Deployments.Steps;
using CrestApps.OrchardCore.AI.Tools.Handlers;
using CrestApps.OrchardCore.Core.Deployments;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Deployments.Sources;

/// <summary>
/// Writes the selected AI tool instances to a deployment plan. The tool instance handlers strip the secrets while each
/// instance is exported.
/// </summary>
internal sealed class AIToolInstanceDeploymentSource : CatalogDeploymentSourceBase<AIToolInstanceDeploymentStep, AIToolInstance>
{
    private readonly INamedCatalog<AIToolInstance> _instancesCatalog;
    private readonly IEnumerable<IAIToolInstanceHandler> _handlers;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolInstanceDeploymentSource"/> class.
    /// </summary>
    /// <param name="instancesCatalog">The catalog used to retrieve the AI tool instances to export.</param>
    /// <param name="handlers">The collection of handlers invoked while each tool instance is exported.</param>
    /// <param name="logger">The logger instance for this source.</param>
    public AIToolInstanceDeploymentSource(
        INamedCatalog<AIToolInstance> instancesCatalog,
        IEnumerable<IAIToolInstanceHandler> handlers,
        ILogger<AIToolInstanceDeploymentSource> logger)
        : base("instances")
    {
        _instancesCatalog = instancesCatalog;
        _handlers = handlers;
        _logger = logger;
    }

    protected override async Task<IEnumerable<AIToolInstance>> GetEntriesAsync()
        => await _instancesCatalog.GetAllAsync();

    protected override bool IsSelected(AIToolInstanceDeploymentStep step, AIToolInstance entry)
        => IsSelectedByKey(step.IncludeAll, step.InstanceIds, entry.ItemId);

    protected override JsonObject Export(AIToolInstance entry)
    {
        var instanceObject = new JsonObject()
        {
            { "ItemId", entry.ItemId },
            { "Source", entry.Source },
            { "Name", entry.Name },
            { "Description", entry.Description },
            { "CreatedUtc", entry.CreatedUtc },
            { "OwnerId", entry.OwnerId },
            { "Author", entry.Author },
            { "Properties", JsonSerializer.SerializeToNode(entry.Properties) },
        };

        var exportingContext = new ExportingAIToolInstanceContext(entry, instanceObject);

        _handlers.Invoke((handler, context) => handler.Exporting(context), exportingContext, _logger);

        return instanceObject;
    }
}

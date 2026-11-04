using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Deployments.Steps;
using CrestApps.OrchardCore.Core.Deployments;

namespace CrestApps.OrchardCore.AI.Deployments.Sources;

/// <summary>
/// Writes the selected AI profile templates to a deployment plan, in the shape <c>AIProfileTemplateStep</c> reads.
/// </summary>
internal sealed class AIProfileTemplateDeploymentSource : CatalogDeploymentSourceBase<AIProfileTemplateDeploymentStep, AIProfileTemplate>
{
    private readonly INamedCatalog<AIProfileTemplate> _templatesCatalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileTemplateDeploymentSource"/> class.
    /// </summary>
    /// <param name="templatesCatalog">The templates catalog.</param>
    public AIProfileTemplateDeploymentSource(INamedCatalog<AIProfileTemplate> templatesCatalog)
        : base("templates")
    {
        _templatesCatalog = templatesCatalog;
    }

    protected override async Task<IEnumerable<AIProfileTemplate>> GetEntriesAsync()
        => await _templatesCatalog.GetAllAsync();

    protected override bool IsSelected(AIProfileTemplateDeploymentStep step, AIProfileTemplate entry)
        => IsSelectedByKey(step.IncludeAll, step.TemplateNames, entry.Name);

    protected override JsonObject Export(AIProfileTemplate entry)
        => new()
        {
            { "ItemId", entry.ItemId },
            { "Name", entry.Name },
            { "Source", entry.Source },
            { "DisplayText", entry.DisplayText },
            { "Description", entry.Description },
            { "Category", entry.Category },
            { "IsListable", entry.IsListable },
            { "CreatedUtc", entry.CreatedUtc },
            { "OwnerId", entry.OwnerId },
            { "Author", entry.Author },
            { "Properties", entry.Properties != null ? JsonSerializer.SerializeToNode(entry.Properties) : null },
        };
}

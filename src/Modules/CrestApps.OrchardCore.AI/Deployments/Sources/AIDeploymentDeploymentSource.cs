using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Deployments.Steps;
using CrestApps.OrchardCore.Core.Deployments;

namespace CrestApps.OrchardCore.AI.Deployments.Sources;

/// <summary>
/// Writes the selected AI deployments to a deployment plan, in the shape <c>AIDeploymentStep</c> reads.
/// </summary>
internal sealed class AIDeploymentDeploymentSource : CatalogDeploymentSourceBase<AIDeploymentDeploymentStep, AIDeployment>
{
    private readonly INamedSourceCatalog<AIDeployment> _deploymentCatalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIDeploymentDeploymentSource"/> class.
    /// </summary>
    /// <param name="deploymentCatalog">The deployment catalog.</param>
    public AIDeploymentDeploymentSource(INamedSourceCatalog<AIDeployment> deploymentCatalog)
        : base("deployments")
    {
        _deploymentCatalog = deploymentCatalog;
    }

    protected override async Task<IEnumerable<AIDeployment>> GetEntriesAsync()
        => await _deploymentCatalog.GetAllAsync();

    protected override bool IsSelected(AIDeploymentDeploymentStep step, AIDeployment entry)
        => IsSelectedByKey(step.IncludeAll, step.DeploymentNames, entry.Name);

    protected override JsonObject Export(AIDeployment entry)
        => new()
        {
            { "ItemId", entry.ItemId },
            { "Name", entry.Name },
            { "ModelName", entry.ModelName },
            { "ClientName", entry.Source },
            { "ProviderName", entry.Source },
            { "ConnectionName", entry.ConnectionName },
            { "Author", entry.Author },
            { "OwnerId", entry.OwnerId },
            { "CreatedUtc", entry.CreatedUtc },
            { "Properties", JsonSerializer.SerializeToNode(entry.Properties) },
        };
}

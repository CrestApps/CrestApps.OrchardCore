using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
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
    // Prices are exported without their empty options, so an exported plan reads cleanly and imports as written.
    private static readonly JsonSerializerOptions _pricingSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly INamedSourceCatalog<AIDeployment> _deploymentCatalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIDeploymentDeploymentSource"/> class.
    /// </summary>
    /// <param name="deploymentCatalog">The deployment catalog.</param>
    public AIDeploymentDeploymentSource(INamedSourceCatalog<AIDeployment> deploymentCatalog)
        // The recipe schema names the list "Deployments"; the step reads either spelling.
        : base("Deployments")
    {
        _deploymentCatalog = deploymentCatalog;
    }

    protected override async Task<IEnumerable<AIDeployment>> GetEntriesAsync()
        => await _deploymentCatalog.GetAllAsync();

    protected override bool IsSelected(AIDeploymentDeploymentStep step, AIDeployment entry)
        => IsSelectedByKey(step.IncludeAll, step.DeploymentNames, entry.Name);

    protected override JsonObject Export(AIDeployment entry)
    {
        // Pricing is exported on its own, readable section rather than inside Properties.
        var properties = JsonSerializer.SerializeToNode(entry.Properties)?.AsObject();
        properties?.Remove(nameof(AIDeploymentPricing));

        var deploymentInfo = new JsonObject()
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
            { "Properties", properties },
        };

        if (entry.GetPricing() is { } pricing)
        {
            deploymentInfo["Pricing"] = JsonSerializer.SerializeToNode(pricing, _pricingSerializerOptions);
        }

        return deploymentInfo;
    }
}

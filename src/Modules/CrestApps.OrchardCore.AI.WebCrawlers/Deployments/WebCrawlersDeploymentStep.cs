using CrestApps.OrchardCore.AI.WebCrawlers.Recipes;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.AI.WebCrawlers.Deployments;

/// <summary>
/// Exports every web crawler as a <c>WebCrawlers</c> recipe step. Secrets in a crawler's settings are never exported.
/// </summary>
public sealed class WebCrawlersDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<WebCrawlersDeploymentStep>("Artificial Intelligence");
    private static readonly LocalizationSource _title = LocalizationSource.Create<WebCrawlersDeploymentStep>("Web crawlers");

    /// <inheritdoc/>
    public override string Name => WebCrawlersStep.StepKey;

    /// <inheritdoc/>
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc/>
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Title => _title;
}

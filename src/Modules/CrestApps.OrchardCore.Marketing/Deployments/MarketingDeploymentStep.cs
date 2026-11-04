using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Marketing.Deployments;

/// <summary>
/// Exports every marketing segment and campaign as a <c>MarketingSegments</c> and a <c>MarketingCampaigns</c> recipe step. Campaigns are exported without their delivery, and import as drafts.
/// </summary>
public sealed class MarketingDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<MarketingDeploymentStep>("Commerce");
    private static readonly LocalizationSource _title = LocalizationSource.Create<MarketingDeploymentStep>("Marketing segments and campaigns");

    /// <inheritdoc/>
    public override string Name => nameof(MarketingDeploymentStep);

    /// <inheritdoc/>
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc/>
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Title => _title;
}

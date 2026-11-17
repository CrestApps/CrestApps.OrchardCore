using System.Text.Json.Serialization;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Banking.Checks.Deployments;

/// <summary>
/// Exports the check layouts, without their signature images or check line font.
/// </summary>
public sealed class CheckLayoutsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<CheckLayoutsDeploymentStep>("Finance");
    private static readonly LocalizationSource _title = LocalizationSource.Create<CheckLayoutsDeploymentStep>("Check layouts");

    /// <inheritdoc />
    public override string Name => ChecksConstants.LayoutsRecipeStepName;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}

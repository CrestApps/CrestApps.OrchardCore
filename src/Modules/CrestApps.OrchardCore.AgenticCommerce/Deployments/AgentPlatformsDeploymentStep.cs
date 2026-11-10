using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.AgenticCommerce.Deployments;

/// <summary>
/// Exports the AI agent platforms, without their credentials.
/// </summary>
public sealed class AgentPlatformsDeploymentStep : DeploymentStep
{
    /// <summary>
    /// The step's name.
    /// </summary>
    public const string StepName = "AgentPlatforms";

    private static readonly LocalizationSource _category = LocalizationSource.Create<AgentPlatformsDeploymentStep>("Commerce");
    private static readonly LocalizationSource _title = LocalizationSource.Create<AgentPlatformsDeploymentStep>("AI agent platforms");

    /// <inheritdoc />
    public override string Name => StepName;

    /// <inheritdoc />
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Title => _title;
}

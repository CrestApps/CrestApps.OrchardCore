using System.Text.Json.Serialization;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Skills.Deployments;

/// <summary>
/// Exports the skill and certification catalog, and optionally the skills people hold and their certificates.
/// </summary>
public sealed class SkillsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<SkillsDeploymentStep>("Workforce");
    private static readonly LocalizationSource _title = LocalizationSource.Create<SkillsDeploymentStep>("Skills and certifications");

    /// <inheritdoc />
    public override string Name => SkillsConstants.RecipeStepName;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Title => _title;

    /// <summary>
    /// Gets or sets whether the skills people hold are exported too.
    /// </summary>
    public bool IncludeAssignments { get; set; }

    /// <summary>
    /// Gets or sets whether people's certificates are exported too.
    /// </summary>
    public bool IncludeCertificationRecords { get; set; }
}

using System.Text.Json.Serialization;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Learning.Assessments.Deployments;

/// <summary>
/// Exports every grade scale.
/// </summary>
public sealed class LearningGradeScalesDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<LearningGradeScalesDeploymentStep>("Learning");
    private static readonly LocalizationSource _title = LocalizationSource.Create<LearningGradeScalesDeploymentStep>("Learning grade scales");

    /// <inheritdoc />
    public override string Name => AssessmentsConstants.GradeScalesRecipeStep;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}

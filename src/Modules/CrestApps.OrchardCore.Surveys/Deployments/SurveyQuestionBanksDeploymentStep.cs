using System.Text.Json.Serialization;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Surveys.Deployments;

/// <summary>
/// Exports question banks with their questions.
/// </summary>
public sealed class SurveyQuestionBanksDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<SurveyQuestionBanksDeploymentStep>("Surveys");
    private static readonly LocalizationSource _title = LocalizationSource.Create<SurveyQuestionBanksDeploymentStep>("Question banks");

    /// <summary>
    /// Gets the step name.
    /// </summary>
    public override string Name => nameof(SurveyQuestionBanksDeploymentStep);

    /// <summary>
    /// Gets or sets whether every bank is exported.
    /// </summary>
    public bool IncludeAll { get; set; } = true;

    /// <summary>
    /// Gets or sets the technical names of the banks exported when not all are.
    /// </summary>
    public string[] BankNames { get; set; } = [];

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}

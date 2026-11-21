using System.Text.Json.Serialization;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Surveys.Deployments;

/// <summary>
/// Exports survey accommodations.
/// </summary>
public sealed class SurveyOverridesDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<SurveyOverridesDeploymentStep>("Surveys");
    private static readonly LocalizationSource _title = LocalizationSource.Create<SurveyOverridesDeploymentStep>("Survey accommodations");

    /// <summary>
    /// Gets the step name.
    /// </summary>
    public override string Name => nameof(SurveyOverridesDeploymentStep);

    /// <summary>
    /// Gets or sets whether the accommodations of every survey are exported.
    /// </summary>
    public bool IncludeAll { get; set; } = true;

    /// <summary>
    /// Gets or sets the technical names of the surveys whose accommodations are exported when not all are.
    /// </summary>
    public string[] SurveyNames { get; set; } = [];

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}

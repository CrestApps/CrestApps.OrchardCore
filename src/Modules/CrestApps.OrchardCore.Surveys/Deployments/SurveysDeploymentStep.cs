using System.Text.Json.Serialization;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Surveys.Deployments;

/// <summary>
/// Exports surveys with their versions and collectors.
/// </summary>
public sealed class SurveysDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<SurveysDeploymentStep>("Surveys");
    private static readonly LocalizationSource _title = LocalizationSource.Create<SurveysDeploymentStep>("Surveys");

    /// <summary>
    /// Gets the step name.
    /// </summary>
    public override string Name => nameof(SurveysDeploymentStep);

    /// <summary>
    /// Gets or sets whether every survey is exported.
    /// </summary>
    public bool IncludeAll { get; set; } = true;

    /// <summary>
    /// Gets or sets the technical names of the surveys exported when not all are.
    /// </summary>
    public string[] SurveyNames { get; set; } = [];

    /// <summary>
    /// Gets or sets whether collector tokens and password hashes are exported. Needs the Export survey responses
    /// permission.
    /// </summary>
    public bool IncludeSecrets { get; set; }

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}

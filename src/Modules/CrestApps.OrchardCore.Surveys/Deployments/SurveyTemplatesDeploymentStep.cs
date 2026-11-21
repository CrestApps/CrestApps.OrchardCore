using System.Text.Json.Serialization;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Surveys.Deployments;

/// <summary>
/// Exports survey templates.
/// </summary>
public sealed class SurveyTemplatesDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<SurveyTemplatesDeploymentStep>("Surveys");
    private static readonly LocalizationSource _title = LocalizationSource.Create<SurveyTemplatesDeploymentStep>("Survey templates");

    /// <inheritdoc />
    public override string Name => nameof(SurveyTemplatesDeploymentStep);

    /// <summary>
    /// Gets or sets whether every template is exported.
    /// </summary>
    public bool IncludeAll { get; set; } = true;

    /// <summary>
    /// Gets or sets the technical names of the templates when not all are.
    /// </summary>
    public string[] TemplateNames { get; set; } = [];

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}

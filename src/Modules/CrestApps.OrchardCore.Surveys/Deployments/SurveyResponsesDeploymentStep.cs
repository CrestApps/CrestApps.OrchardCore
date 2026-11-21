using System.Text.Json.Serialization;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Surveys.Deployments;

/// <summary>
/// Exports survey responses. It needs the Export survey responses permission.
/// </summary>
public sealed class SurveyResponsesDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<SurveyResponsesDeploymentStep>("Surveys");
    private static readonly LocalizationSource _title = LocalizationSource.Create<SurveyResponsesDeploymentStep>("Survey responses");

    /// <summary>
    /// Gets the step name.
    /// </summary>
    public override string Name => nameof(SurveyResponsesDeploymentStep);

    /// <summary>
    /// Gets or sets whether the responses of every survey are exported.
    /// </summary>
    public bool IncludeAll { get; set; } = true;

    /// <summary>
    /// Gets or sets the technical names of the surveys whose responses are exported when not all are.
    /// </summary>
    public string[] SurveyNames { get; set; } = [];

    /// <summary>
    /// Gets or sets the first day responses started on, or <see langword="null"/>.
    /// </summary>
    public DateOnly? FromDate { get; set; }

    /// <summary>
    /// Gets or sets the last day responses started on, or <see langword="null"/>.
    /// </summary>
    public DateOnly? ToDate { get; set; }

    /// <summary>
    /// Gets or sets whether test responses are exported.
    /// </summary>
    public bool IncludeTest { get; set; }

    /// <summary>
    /// Gets or sets whether responses in progress are exported.
    /// </summary>
    public bool IncludeInProgress { get; set; }

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}

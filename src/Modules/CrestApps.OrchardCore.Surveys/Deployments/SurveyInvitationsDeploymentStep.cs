using System.Text.Json.Serialization;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Surveys.Deployments;

/// <summary>
/// Exports survey invitations; tokens travel only as their hashes.
/// </summary>
public sealed class SurveyInvitationsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<SurveyInvitationsDeploymentStep>("Surveys");
    private static readonly LocalizationSource _title = LocalizationSource.Create<SurveyInvitationsDeploymentStep>("Survey invitations");

    /// <summary>
    /// Gets the step name.
    /// </summary>
    public override string Name => nameof(SurveyInvitationsDeploymentStep);

    /// <summary>
    /// Gets or sets whether the invitations of every survey are exported.
    /// </summary>
    public bool IncludeAll { get; set; } = true;

    /// <summary>
    /// Gets or sets the technical names of the surveys whose invitations are exported when not all are.
    /// </summary>
    public string[] SurveyNames { get; set; } = [];

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}

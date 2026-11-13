using System.Text.Json.Serialization;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Timeline.Deployments;

/// <summary>
/// Exports the timeline entries of some kinds of records.
/// </summary>
public sealed class TimelineEntriesDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<TimelineEntriesDeploymentStep>("Content");
    private static readonly LocalizationSource _title = LocalizationSource.Create<TimelineEntriesDeploymentStep>("Timeline entries");

    /// <inheritdoc />
    public override string Name => TimelineConstants.RecipeStepName;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Title => _title;

    /// <summary>
    /// Gets or sets the record kinds whose entries are exported, comma-separated, such as <c>WorkOrder,Order</c>.
    /// </summary>
    public string SubjectKinds { get; set; }
}

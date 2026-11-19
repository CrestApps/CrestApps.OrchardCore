namespace CrestApps.OrchardCore.Timeline.Deployments;

/// <summary>
/// The settings of the timeline entries deployment step.
/// </summary>
public class TimelineEntriesDeploymentStepViewModel
{
    /// <summary>
    /// Gets or sets the record kinds whose entries are exported, comma-separated.
    /// </summary>
    public string SubjectKinds { get; set; }
}

namespace CrestApps.OrchardCore.ContactCenter.Core.Models.Reports;

/// <summary>
/// Represents the progress of a single subject type's activities: how many are completed versus how
/// many remain pending or in progress.
/// </summary>
public sealed class SubjectInventoryRow
{
    /// <summary>
    /// Gets or sets the subject content type. An empty value represents activities with no subject.
    /// </summary>
    public string SubjectContentType { get; set; }

    /// <summary>
    /// Gets or sets the activity progress counts for the subject.
    /// </summary>
    public ActivityProgressCounts Counts { get; set; } = new();
}

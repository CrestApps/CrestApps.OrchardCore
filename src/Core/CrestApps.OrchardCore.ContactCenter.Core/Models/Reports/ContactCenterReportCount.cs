namespace CrestApps.OrchardCore.ContactCenter.Core.Models.Reports;

/// <summary>
/// Represents a single labeled count used by Contact Center report breakdowns such as volume by
/// channel, direction, status, or source.
/// </summary>
public sealed class ContactCenterReportCount
{
    /// <summary>
    /// Gets or sets the human-readable label the count is grouped under.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the number of items in the group.
    /// </summary>
    public long Count { get; set; }
}

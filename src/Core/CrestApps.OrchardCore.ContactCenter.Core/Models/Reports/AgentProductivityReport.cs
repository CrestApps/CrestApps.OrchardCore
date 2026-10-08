namespace CrestApps.OrchardCore.ContactCenter.Core.Models.Reports;

/// <summary>
/// Represents the agent productivity report: per-agent handled volume, talk time, and completed work
/// over a reporting period.
/// </summary>
public sealed class AgentProductivityReport
{
    /// <summary>
    /// Gets or sets the inclusive lower UTC bound of the reporting period.
    /// </summary>
    public DateTime FromUtc { get; set; }

    /// <summary>
    /// Gets or sets the inclusive upper UTC bound of the reporting period.
    /// </summary>
    public DateTime ToUtc { get; set; }

    /// <summary>
    /// Gets or sets the per-agent productivity rows, ordered by handled volume.
    /// </summary>
    public IList<AgentProductivityRow> Rows { get; set; } = [];
}

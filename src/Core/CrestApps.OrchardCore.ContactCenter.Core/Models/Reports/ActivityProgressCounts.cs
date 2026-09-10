namespace CrestApps.OrchardCore.ContactCenter.Core.Models.Reports;

/// <summary>
/// Represents the progress breakdown of a set of CRM activities used by the campaign summary and
/// subject inventory reports to show what is completed versus what is still pending.
/// </summary>
public sealed class ActivityProgressCounts
{
    /// <summary>
    /// Gets or sets the total number of activities in the group.
    /// </summary>
    public long Total { get; set; }

    /// <summary>
    /// Gets or sets the number of activities that reached a completed disposition.
    /// </summary>
    public long Completed { get; set; }

    /// <summary>
    /// Gets or sets the number of activities that have not been started yet (pending work inventory).
    /// </summary>
    public long Pending { get; set; }

    /// <summary>
    /// Gets or sets the number of activities that are actively being worked (reserved, dialing, or in progress).
    /// </summary>
    public long InProgress { get; set; }

    /// <summary>
    /// Gets or sets the number of activities that failed.
    /// </summary>
    public long Failed { get; set; }

    /// <summary>
    /// Gets or sets the number of activities that were cancelled or purged.
    /// </summary>
    public long Cancelled { get; set; }

    /// <summary>
    /// Gets or sets the total number of contact attempts recorded across the group.
    /// </summary>
    public long TotalAttempts { get; set; }

    /// <summary>
    /// Gets the fraction of activities that are completed, between 0 and 1.
    /// </summary>
    public double CompletionRate
    {
        get
        {
            if (Total <= 0)
            {
                return 0d;
            }

            return (double)Completed / Total;
        }
    }
}

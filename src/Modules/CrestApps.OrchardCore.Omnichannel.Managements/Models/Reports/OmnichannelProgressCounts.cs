namespace CrestApps.OrchardCore.Omnichannel.Managements.Models.Reports;

/// <summary>
/// Represents the completed-versus-pending progress of a set of CRM activities.
/// </summary>
public sealed class OmnichannelProgressCounts
{
    /// <summary>
    /// Gets or sets the total number of activities.
    /// </summary>
    public long Total { get; set; }

    /// <summary>
    /// Gets or sets the number of completed activities.
    /// </summary>
    public long Completed { get; set; }

    /// <summary>
    /// Gets or sets the number of activities that have not been started yet.
    /// </summary>
    public long Pending { get; set; }

    /// <summary>
    /// Gets or sets the number of activities that are actively being worked.
    /// </summary>
    public long InProgress { get; set; }

    /// <summary>
    /// Gets or sets the number of failed activities.
    /// </summary>
    public long Failed { get; set; }

    /// <summary>
    /// Gets or sets the number of cancelled or purged activities.
    /// </summary>
    public long Cancelled { get; set; }

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

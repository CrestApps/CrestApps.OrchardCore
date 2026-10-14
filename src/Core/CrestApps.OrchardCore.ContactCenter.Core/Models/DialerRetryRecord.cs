namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Recorded on a completed dialer campaign record once its next attempt has been scheduled, so a second request to
/// retry it, from a workflow or from the dialer itself, does not create the next attempt twice.
/// </summary>
public sealed class DialerRetryRecord
{
    /// <summary>
    /// Gets or sets the identifier of the next attempt's activity.
    /// </summary>
    public string NextActivityItemId { get; set; }

    /// <summary>
    /// Gets or sets the next attempt's number.
    /// </summary>
    public int AttemptNumber { get; set; }

    /// <summary>
    /// Gets or sets when the next attempt is due.
    /// </summary>
    public DateTime DueUtc { get; set; }

    /// <summary>
    /// Gets or sets what scheduled the next attempt.
    /// </summary>
    public string CreatedBy { get; set; }
}

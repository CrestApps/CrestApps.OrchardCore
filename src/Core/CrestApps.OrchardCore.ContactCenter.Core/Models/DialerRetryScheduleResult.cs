namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// What became of a request to schedule the next attempt of a dialer campaign record.
/// </summary>
public sealed class DialerRetryScheduleResult
{
    /// <summary>
    /// Gets or sets the outcome of the request.
    /// </summary>
    public DialerRetryScheduleStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the next attempt's activity identifier, when one was created.
    /// </summary>
    public string NextActivityItemId { get; set; }

    /// <summary>
    /// Gets or sets the next attempt's number, when one was created.
    /// </summary>
    public int? AttemptNumber { get; set; }

    /// <summary>
    /// Gets or sets when the next attempt is due, when one was created.
    /// </summary>
    public DateTime? DueUtc { get; set; }

    /// <summary>
    /// Gets or sets why no attempt was created.
    /// </summary>
    public string Reason { get; set; }
}

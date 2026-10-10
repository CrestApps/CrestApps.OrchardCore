namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Describes one activity waiting in a campaign's dialer queue, with the dialer profile it is queued under.
/// </summary>
public sealed class ActivityDialerWaitingRecord
{
    /// <summary>
    /// Gets or sets the activity identifier.
    /// </summary>
    public string ActivityId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the dialer profile the activity is queued under.
    /// </summary>
    public string ProfileId { get; set; }
}

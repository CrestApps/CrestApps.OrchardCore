namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Represents a request to disposition an omnichannel activity.
/// </summary>
public sealed class ActivityDispositionRequest
{
    /// <summary>
    /// Gets or sets the activity to disposition.
    /// </summary>
    public OmnichannelActivity Activity { get; set; }

    /// <summary>
    /// Gets or sets the selected disposition identifier.
    /// </summary>
    public string DispositionId { get; set; }

    /// <summary>
    /// Gets or sets the source that produced the disposition.
    /// </summary>
    public ActivityDispositionSource Source { get; set; } = ActivityDispositionSource.Agent;

    /// <summary>
    /// Gets or sets optional notes to append or store with the activity disposition.
    /// </summary>
    public string Notes { get; set; }

    /// <summary>
    /// Gets or sets schedule dates supplied for disposition-driven subject actions.
    /// </summary>
    public IDictionary<string, DateTime?> ActionScheduleDates { get; set; }

    /// <summary>
    /// Gets or sets the actor identifier applying the disposition.
    /// </summary>
    public string ActorId { get; set; }

    /// <summary>
    /// Gets or sets the actor display name applying the disposition.
    /// </summary>
    public string ActorDisplayName { get; set; }
}

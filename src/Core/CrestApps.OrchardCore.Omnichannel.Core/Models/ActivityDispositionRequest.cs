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
    /// Gets or sets who dispositioned the activity, recorded on <see cref="OmnichannelActivity.DispositionedBy"/>.
    /// Leave it unset to derive it from <see cref="Source"/>; set it when the source alone cannot tell, such as the
    /// dialer completing an attempt as a <see cref="ActivityDispositionSource.System"/> outcome.
    /// </summary>
    public ActivityDispositionActor? DispositionedBy { get; set; }

    /// <summary>
    /// Gets or sets optional notes to append or store with the activity disposition.
    /// </summary>
    public string Notes { get; set; }

    /// <summary>
    /// Gets or sets schedule dates supplied for disposition-driven subject actions.
    /// </summary>
    public IDictionary<string, DateTime?> ActionScheduleDates { get; set; }

    /// <summary>
    /// Gets or sets optional preparation notes supplied for disposition-driven subject actions. Each note becomes the
    /// instructions of the follow-up activity created by the matching subject action.
    /// </summary>
    public IDictionary<string, string> ActionPreparationNotes { get; set; }

    /// <summary>
    /// Gets or sets what found the number out of service when the platform chose a not-in-service disposition on its
    /// own, one of <see cref="OmnichannelConstants.NotInServiceSources"/>. Left unset for a person's own disposition.
    /// </summary>
    public string NotInServiceSource { get; set; }

    /// <summary>
    /// Gets or sets the actor identifier applying the disposition.
    /// </summary>
    public string ActorId { get; set; }

    /// <summary>
    /// Gets or sets the actor display name applying the disposition.
    /// </summary>
    public string ActorDisplayName { get; set; }
}

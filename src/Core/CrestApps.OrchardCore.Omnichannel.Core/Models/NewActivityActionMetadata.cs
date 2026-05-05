namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Metadata for the "NewActivity" campaign action type.
/// Stored via Entity Put/TryGet on <see cref="CampaignAction"/>.
/// </summary>
public sealed class NewActivityActionMetadata
{
    /// <summary>
    /// Gets or sets the target campaign identifier for the new activity.
    /// When null, the current campaign is used.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets the content type for the subject of the new activity.
    /// </summary>
    public string SubjectContentType { get; set; }

    /// <summary>
    /// Gets or sets the urgency level for the new activity.
    /// When null, the original activity's urgency level is used.
    /// </summary>
    public ActivityUrgencyLevel? UrgencyLevel { get; set; }

    /// <summary>
    /// Gets or sets the normalized username to assign the new activity to.
    /// When null, the activity is assigned to the user who completed the original activity.
    /// </summary>
    public string NormalizedUserName { get; set; }

    /// <summary>
    /// Gets or sets the default number of hours to schedule the new activity ahead.
    /// Used when no schedule date is provided by the user during completion.
    /// </summary>
    public int? DefaultScheduleHours { get; set; }
}

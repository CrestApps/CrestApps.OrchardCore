namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Metadata for the "NewActivity" action type.
/// Stored via Put/TryGet on <see cref="SubjectAction"/>.
/// </summary>
public sealed class NewActivityActionMetadata
{
    /// <summary>
    /// Gets or sets the target subject content type for the new activity.
    /// When null, the current subject content type is used.
    /// The target subject's flow settings determine the campaign, channel, and interaction type.
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

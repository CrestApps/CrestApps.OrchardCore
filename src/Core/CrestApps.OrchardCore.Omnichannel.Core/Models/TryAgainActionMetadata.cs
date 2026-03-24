namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Metadata for the "TryAgain" campaign action type.
/// Stored via Entity Put/TryGet on <see cref="CampaignAction"/>.
/// </summary>
public sealed class TryAgainActionMetadata
{
    /// <summary>
    /// Gets or sets the maximum number of retry attempts allowed.
    /// When null, unlimited retries are allowed.
    /// </summary>
    public int? MaxAttempt { get; set; }

    /// <summary>
    /// Gets or sets the urgency level for the retry activity.
    /// When null, the original activity's urgency level is used.
    /// </summary>
    public ActivityUrgencyLevel? UrgencyLevel { get; set; }

    /// <summary>
    /// Gets or sets the normalized username to assign the retry activity to.
    /// When null, the activity is assigned to the user who completed the original activity.
    /// </summary>
    public string NormalizedUserName { get; set; }

    /// <summary>
    /// Gets or sets the default number of hours to schedule the retry activity ahead.
    /// Used when no schedule date is provided by the user during completion.
    /// </summary>
    public int? DefaultScheduleHours { get; set; }
}

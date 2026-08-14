namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Metadata for the "TryAgain" subject action type.
/// Stored via Entity Put/TryGet on <see cref="SubjectAction"/>.
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
    /// Gets or sets how the owner of the retry activity is selected.
    /// </summary>
    public SubjectActionOwnerAssignmentType AssignmentType { get; set; }

    /// <summary>
    /// Gets or sets the normalized username to assign the retry activity to.
    /// Used when <see cref="AssignmentType"/> is <see cref="SubjectActionOwnerAssignmentType.SpecificOwner"/>.
    /// </summary>
    public string NormalizedUserName { get; set; }

    /// <summary>
    /// Gets or sets the default number of hours to schedule the retry activity ahead.
    /// Used when no schedule date is provided by the user during completion.
    /// </summary>
    public int? DefaultScheduleHours { get; set; }
}

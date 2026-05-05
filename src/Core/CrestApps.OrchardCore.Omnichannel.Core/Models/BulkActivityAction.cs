namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Specifies the bulk action types that can be applied to omnichannel activities.
/// </summary>
public enum BulkActivityAction
{
    /// <summary>
    /// No action selected.
    /// </summary>
    None,

    /// <summary>
    /// Assign or reassign activities to one or more users.
    /// </summary>
    Assign,

    /// <summary>
    /// Reschedule activities to a new date and time.
    /// </summary>
    Reschedule,

    /// <summary>
    /// Purge activities by setting their status to Purged.
    /// </summary>
    Purge,

    /// <summary>
    /// Set instructions for all selected activities.
    /// </summary>
    SetInstructions,

    /// <summary>
    /// Set the urgency level for all selected activities.
    /// </summary>
    SetUrgencyLevel,

    /// <summary>
    /// Change the subject content type for all selected activities.
    /// </summary>
    ChangeSubject,
}

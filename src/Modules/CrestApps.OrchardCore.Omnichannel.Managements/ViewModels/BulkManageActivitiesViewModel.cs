using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Represents the view model for binding bulk action form data.
/// </summary>
public sealed class BulkManageActivitiesViewModel
{
    /// <summary>
    /// Gets or sets the selected activity item IDs for bulk action.
    /// </summary>
    public string[] ItemIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the bulk action to perform.
    /// </summary>
    public BulkActivityAction BulkAction { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the action should apply to all matching activities.
    /// </summary>
    public bool ApplyToAllMatching { get; set; }

    /// <summary>
    /// Gets or sets the user IDs to assign activities to (for Assign action).
    /// </summary>
    public string[] AssignToUserIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the new scheduled date (for Reschedule action).
    /// </summary>
    public string NewScheduledDate { get; set; }

    /// <summary>
    /// Gets or sets the instructions text (for SetInstructions action).
    /// </summary>
    public string Instructions { get; set; }

    /// <summary>
    /// Gets or sets the urgency level (for SetUrgencyLevel action).
    /// </summary>
    public ActivityUrgencyLevel? NewUrgencyLevel { get; set; }

    /// <summary>
    /// Gets or sets the new subject content type (for ChangeSubject action).
    /// </summary>
    public string NewSubjectContentType { get; set; }
}

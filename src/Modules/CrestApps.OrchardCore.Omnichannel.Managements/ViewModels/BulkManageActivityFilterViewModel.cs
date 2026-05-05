using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Represents the view model for the bulk manage activity filter.
/// </summary>
public class BulkManageActivityFilterViewModel
{
    /// <summary>
    /// Gets or sets whether to filter by published contacts.
    /// </summary>
    public string ContactIsPublished { get; set; }

    /// <summary>
    /// Gets or sets the attempt filter expression.
    /// </summary>
    public string AttemptFilter { get; set; }

    /// <summary>
    /// Gets or sets the subject content type filter.
    /// </summary>
    public string SubjectContentType { get; set; }

    /// <summary>
    /// Gets or sets the channel filter.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets the assigned to user IDs filter.
    /// </summary>
    public string[] AssignedToUserIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the scheduled from date filter.
    /// </summary>
    public string ScheduledFrom { get; set; }

    /// <summary>
    /// Gets or sets the scheduled to date filter.
    /// </summary>
    public string ScheduledTo { get; set; }

    /// <summary>
    /// Gets or sets the created from date filter.
    /// </summary>
    public string CreatedFrom { get; set; }

    /// <summary>
    /// Gets or sets the created to date filter.
    /// </summary>
    public string CreatedTo { get; set; }

    /// <summary>
    /// Gets or sets the urgency level filter.
    /// </summary>
    public string UrgencyLevel { get; set; }

    /// <summary>
    /// Gets or sets the available contact published options.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> ContactPublishedOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the available attempt filter options.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> AttemptFilters { get; set; } = [];

    /// <summary>
    /// Gets or sets the available subject content types.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> SubjectContentTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets the available channels.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Channels { get; set; } = [];

    /// <summary>
    /// Gets or sets the available urgency levels.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> UrgencyLevels { get; set; } = [];

    /// <summary>
    /// Gets or sets the user search endpoint URL for the item selector.
    /// </summary>
    [BindNever]
    public string UserSearchEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the JSON representation of the currently selected assigned users for the item selector.
    /// </summary>
    [BindNever]
    public string SelectedAssignedUsersJson { get; set; } = "[]";
}

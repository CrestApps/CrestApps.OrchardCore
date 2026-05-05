using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Represents the view model for the bulk activity actions panel.
/// </summary>
public class BulkActivityActionsViewModel
{
    /// <summary>
    /// Gets or sets the available urgency levels for the bulk action.
    /// </summary>
    public IEnumerable<SelectListItem> UrgencyLevels { get; set; } = [];

    /// <summary>
    /// Gets or sets the available subject content types for the bulk action.
    /// </summary>
    public IEnumerable<SelectListItem> SubjectContentTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets the user search endpoint URL.
    /// </summary>
    public string UserSearchEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the total number of activities matching the current filter.
    /// </summary>
    public int TotalCount { get; set; }
}

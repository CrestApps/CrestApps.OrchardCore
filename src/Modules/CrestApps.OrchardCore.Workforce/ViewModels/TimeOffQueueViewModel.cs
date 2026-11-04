using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// The time-off queue: its filters, the requests and the bulk actions.
/// </summary>
public class TimeOffQueueViewModel : WorkforceListViewModel
{
    /// <summary>
    /// Gets or sets the worker the list is for.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the bulk actions.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> BulkActions { get; set; } = [];
}

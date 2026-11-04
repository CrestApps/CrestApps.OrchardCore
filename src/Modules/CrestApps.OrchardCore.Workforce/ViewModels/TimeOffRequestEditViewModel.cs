using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// The form a manager requests time off with on a worker's behalf.
/// </summary>
public class TimeOffRequestEditViewModel
{
    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the time-off type.
    /// </summary>
    public string TypeId { get; set; }

    /// <summary>
    /// Gets or sets the first day.
    /// </summary>
    public DateOnly? StartDate { get; set; }

    /// <summary>
    /// Gets or sets the last day.
    /// </summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>
    /// Gets or sets whether only part of one day is asked for.
    /// </summary>
    public bool PartialDay { get; set; }

    /// <summary>
    /// Gets or sets when part of a day starts.
    /// </summary>
    public string StartTime { get; set; }

    /// <summary>
    /// Gets or sets when part of a day ends.
    /// </summary>
    public string EndTime { get; set; }

    /// <summary>
    /// Gets or sets the reason.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets what the request would count and do, once checked.
    /// </summary>
    [BindNever]
    public TimeOffPreview Preview { get; set; }

    /// <summary>
    /// Gets or sets the worker's name, once chosen.
    /// </summary>
    [BindNever]
    public string WorkerName { get; set; }

    /// <summary>
    /// Gets or sets the workers the user manages.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> WorkerOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the time-off types.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TypeOptions { get; set; } = [];
}

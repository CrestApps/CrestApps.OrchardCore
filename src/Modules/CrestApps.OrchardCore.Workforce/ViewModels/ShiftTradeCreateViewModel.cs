using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// The form that offers a trade on a worker's behalf.
/// </summary>
public class ShiftTradeCreateViewModel
{
    /// <summary>
    /// Gets or sets the worker offering a shift.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets what the trade does, <c>GiveAway</c> or <c>Swap</c>.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets the shift offered.
    /// </summary>
    public string ShiftEventId { get; set; }

    /// <summary>
    /// Gets or sets the colleague it is offered to.
    /// </summary>
    public string TargetWorkerId { get; set; }

    /// <summary>
    /// Gets or sets the colleague's shift, for a swap.
    /// </summary>
    public string TargetShiftEventId { get; set; }

    /// <summary>
    /// Gets or sets a note to the colleague.
    /// </summary>
    public string Note { get; set; }

    /// <summary>
    /// Gets or sets the workers to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> WorkerOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the worker's upcoming shifts.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> ShiftOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the colleagues to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TargetOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the colleague's upcoming shifts.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TargetShiftOptions { get; set; } = [];
}

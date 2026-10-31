using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// The activity code editor.
/// </summary>
public class ActivityCodeEditViewModel
{
    /// <summary>
    /// Gets or sets the code id, or empty for a new code.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the short code.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets the kind.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets the color.
    /// </summary>
    public string Color { get; set; }

    /// <summary>
    /// Gets or sets whether the time is paid.
    /// </summary>
    public bool IsPaid { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the time counts as work.
    /// </summary>
    public bool CountsAsWork { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the worker counts toward staffing during it.
    /// </summary>
    public bool CountsTowardCoverage { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the code can be scheduled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the kinds to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> KindOptions { get; set; } = [];
}

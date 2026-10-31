using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// The editor of a draft shift.
/// </summary>
public class DraftShiftEditViewModel
{
    /// <summary>
    /// Gets or sets the draft shift id; empty for a new shift.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the schedule.
    /// </summary>
    public string PeriodId { get; set; }

    /// <summary>
    /// Gets or sets the worker; empty for an open shift.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the day.
    /// </summary>
    public DateOnly? Date { get; set; }

    /// <summary>
    /// Gets or sets the shift template the times come from.
    /// </summary>
    public string TemplateId { get; set; }

    /// <summary>
    /// Gets or sets when the shift starts, as HH:mm.
    /// </summary>
    public string Start { get; set; }

    /// <summary>
    /// Gets or sets when the shift ends, as HH:mm; at or before the start for the next day.
    /// </summary>
    public string End { get; set; }

    /// <summary>
    /// Gets or sets the breaks and activities, one per line.
    /// </summary>
    public string Segments { get; set; }

    /// <summary>
    /// Gets or sets notes for the worker.
    /// </summary>
    public string Notes { get; set; }

    /// <summary>
    /// Gets or sets the schedule's name.
    /// </summary>
    [BindNever]
    public string PeriodName { get; set; }

    /// <summary>
    /// Gets or sets the names of the activity codes, for the hint.
    /// </summary>
    [BindNever]
    public IList<string> ActivityNames { get; set; } = [];

    /// <summary>
    /// Gets or sets the workers to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> WorkerOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the shift templates to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TemplateOptions { get; set; } = [];
}

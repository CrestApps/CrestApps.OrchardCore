using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// The form that posts an open shift on its own.
/// </summary>
public class OpenShiftEditViewModel
{
    /// <summary>
    /// Gets or sets the team.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the day.
    /// </summary>
    public DateOnly? Date { get; set; }

    /// <summary>
    /// Gets or sets the template.
    /// </summary>
    public string TemplateId { get; set; }

    /// <summary>
    /// Gets or sets when it starts, as <c>HH:mm</c>.
    /// </summary>
    public string Start { get; set; }

    /// <summary>
    /// Gets or sets when it ends, as <c>HH:mm</c>.
    /// </summary>
    public string End { get; set; }

    /// <summary>
    /// Gets or sets the breaks and activities, one per line.
    /// </summary>
    public string Segments { get; set; }

    /// <summary>
    /// Gets or sets the notes.
    /// </summary>
    public string Notes { get; set; }

    /// <summary>
    /// Gets or sets the names of the activity codes.
    /// </summary>
    [BindNever]
    public IList<string> ActivityNames { get; set; } = [];

    /// <summary>
    /// Gets or sets the teams to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TeamOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the templates to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TemplateOptions { get; set; } = [];
}

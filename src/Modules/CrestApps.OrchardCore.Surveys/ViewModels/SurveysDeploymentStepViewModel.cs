using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The editor of a Surveys deployment step.
/// </summary>
public class SurveysDeploymentStepViewModel
{
    /// <summary>
    /// Gets or sets whether every record is exported.
    /// </summary>
    public bool IncludeAll { get; set; } = true;

    /// <summary>
    /// Gets or sets the technical names selected.
    /// </summary>
    public string[] Names { get; set; } = [];

    /// <summary>
    /// Gets or sets whether collector secrets are exported.
    /// </summary>
    public bool IncludeSecrets { get; set; }

    /// <summary>
    /// Gets or sets the first day.
    /// </summary>
    public DateOnly? FromDate { get; set; }

    /// <summary>
    /// Gets or sets the last day.
    /// </summary>
    public DateOnly? ToDate { get; set; }

    /// <summary>
    /// Gets or sets whether test responses are exported.
    /// </summary>
    public bool IncludeTest { get; set; }

    /// <summary>
    /// Gets or sets whether responses in progress are exported.
    /// </summary>
    public bool IncludeInProgress { get; set; }

    /// <summary>
    /// Gets or sets the records to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Options { get; set; } = [];
}

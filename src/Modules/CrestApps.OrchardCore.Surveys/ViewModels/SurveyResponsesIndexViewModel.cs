using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The responses list of a survey.
/// </summary>
public class SurveyResponsesIndexViewModel
{
    /// <summary>
    /// Gets or sets the survey.
    /// </summary>
    [BindNever]
    public Survey Survey { get; set; }

    /// <summary>
    /// Gets or sets the search on the response number.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets the status filter.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the version filter.
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    /// Gets or sets the collector filter.
    /// </summary>
    public string Collector { get; set; }

    /// <summary>
    /// Gets or sets whether test responses show.
    /// </summary>
    public bool IncludeTest { get; set; }

    /// <summary>
    /// Gets or sets the bulk action.
    /// </summary>
    public string BulkAction { get; set; }

    /// <summary>
    /// Gets or sets the status options.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> StatusOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the version options.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> VersionOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the collector options.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> CollectorOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the bulk action options.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> BulkActions { get; set; } = [];

    /// <summary>
    /// Gets or sets the list.
    /// </summary>
    [BindNever]
    public AdminListContext List { get; set; }

    /// <summary>
    /// Gets or sets, when an anonymous survey hides its responses because the filters select too few, the minimum group
    /// size they must reach; otherwise 0.
    /// </summary>
    [BindNever]
    public int HiddenBelow { get; set; }
}

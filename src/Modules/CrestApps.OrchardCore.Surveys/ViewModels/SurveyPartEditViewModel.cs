using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The editor of an embedded survey.
/// </summary>
public class SurveyPartEditViewModel
{
    /// <summary>
    /// Gets or sets the id of the survey.
    /// </summary>
    public string SurveyId { get; set; }

    /// <summary>
    /// Gets or sets the id of the collector, or empty for the default link.
    /// </summary>
    public string CollectorId { get; set; }

    /// <summary>
    /// Gets or sets how the survey shows.
    /// </summary>
    public SurveyEmbedDisplay Display { get; set; }

    /// <summary>
    /// Gets or sets the text of the button or link.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets the surveys offered.
    /// </summary>
    [BindNever]
    public List<SelectListItem> Surveys { get; set; } = [];

    /// <summary>
    /// Gets or sets the collectors of the chosen survey.
    /// </summary>
    [BindNever]
    public List<SelectListItem> Collectors { get; set; } = [];
}

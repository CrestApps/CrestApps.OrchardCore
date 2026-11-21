using CrestApps.OrchardCore.Surveys.Models;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The survey template list.
/// </summary>
public class SurveyTemplateIndexViewModel
{
    /// <summary>
    /// Gets or sets the search text.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets the listed templates.
    /// </summary>
    public AdminListContext<SurveyTemplate> List { get; set; }
}

using CrestApps.OrchardCore.Surveys.Models;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The collectors page.
/// </summary>
public class SurveyCollectorsViewModel
{
    /// <summary>
    /// Gets or sets the survey.
    /// </summary>
    public Survey Survey { get; set; }

    /// <summary>
    /// Gets or sets the list.
    /// </summary>
    public AdminListContext List { get; set; }
}

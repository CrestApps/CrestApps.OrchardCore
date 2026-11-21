using CrestApps.OrchardCore.Surveys.Models;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The versions page.
/// </summary>
public class SurveyVersionsViewModel
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

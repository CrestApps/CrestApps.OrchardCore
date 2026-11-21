using CrestApps.OrchardCore.Surveys.Models;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// A page that shows an editor of a survey, such as the access settings.
/// </summary>
public class SurveyEditorPageViewModel
{
    /// <summary>
    /// Gets or sets the survey.
    /// </summary>
    public Survey Survey { get; set; }

    /// <summary>
    /// Gets or sets the editor.
    /// </summary>
    public IShape Editor { get; set; }
}

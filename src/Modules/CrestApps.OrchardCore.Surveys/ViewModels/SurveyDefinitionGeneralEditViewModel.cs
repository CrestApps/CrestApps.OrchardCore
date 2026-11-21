using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The general, presentation and navigation settings of a survey definition.
/// </summary>
public class SurveyDefinitionGeneralEditViewModel
{
    /// <summary>
    /// Gets or sets the title respondents see.
    /// </summary>
    public string DefinitionTitle { get; set; }

    /// <summary>
    /// Gets or sets the introduction.
    /// </summary>
    public string Introduction { get; set; }

    /// <summary>
    /// Gets or sets the presentation settings.
    /// </summary>
    public SurveyPresentationSettings Presentation { get; set; } = new();

    /// <summary>
    /// Gets or sets the navigation settings.
    /// </summary>
    public SurveyNavigationSettings Navigation { get; set; } = new();
}

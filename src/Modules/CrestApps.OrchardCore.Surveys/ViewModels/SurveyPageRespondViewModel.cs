using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The model of the <c>SurveyPage_Respond</c> shape: one screen of a response.
/// </summary>
public class SurveyPageRespondViewModel
{
    /// <summary>
    /// Gets or sets the survey.
    /// </summary>
    public Survey Survey { get; set; }

    /// <summary>
    /// Gets or sets the respondent title of the survey.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the response.
    /// </summary>
    public SurveyResponse Response { get; set; }

    /// <summary>
    /// Gets or sets the screen.
    /// </summary>
    public SurveyPageView Page { get; set; }

    /// <summary>
    /// Gets or sets the presentation settings.
    /// </summary>
    public SurveyPresentationSettings Presentation { get; set; } = new();

    /// <summary>
    /// Gets or sets the question shapes in plan order.
    /// </summary>
    public IList<IShape> Questions { get; set; } = [];

    /// <summary>
    /// Gets or sets the error message of the post, or <see langword="null"/>.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets whether answers are checked one at a time.
    /// </summary>
    public bool ImmediateFeedback { get; set; }

    /// <summary>
    /// Gets or sets whether respondents confirm before they finish.
    /// </summary>
    public bool ConfirmBeforeSubmit { get; set; }

    /// <summary>
    /// Gets or sets whether this is a staff preview.
    /// </summary>
    public bool IsPreview { get; set; }

    /// <summary>
    /// Gets or sets whether a preview shows hidden questions.
    /// </summary>
    public bool ShowHidden { get; set; }

    /// <summary>
    /// Gets or sets the screens a preview may jump to: id and title.
    /// </summary>
    public IList<KeyValuePair<string, string>> Screens { get; set; } = [];
}

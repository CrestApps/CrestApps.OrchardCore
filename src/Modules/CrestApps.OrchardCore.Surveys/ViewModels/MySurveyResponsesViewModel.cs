using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The signed-in respondent's own responses.
/// </summary>
public class MySurveyResponsesViewModel
{
    /// <summary>
    /// Gets or sets the responses with their surveys and the title of the version each was given on (never the
    /// unpublished draft), newest first.
    /// </summary>
    public IList<(SurveyResponse Response, Survey Survey, string Title)> Items { get; set; } = [];
}

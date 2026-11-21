namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The logic of one page in plain words.
/// </summary>
public class SurveyLogicPageViewModel
{
    /// <summary>
    /// Gets or sets the page title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the sentences.
    /// </summary>
    public List<string> Lines { get; set; } = [];
}

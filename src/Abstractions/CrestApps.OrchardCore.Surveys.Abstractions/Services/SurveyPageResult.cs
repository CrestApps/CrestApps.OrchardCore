namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// The result of showing or posting a page.
/// </summary>
public sealed class SurveyPageResult : SurveyResponseResult
{
    /// <summary>
    /// Gets or sets the page to show, or <see langword="null"/> when the response ended.
    /// </summary>
    public SurveyPageView Page { get; set; }

    /// <summary>
    /// Gets or sets the errors of the posted answers, by question id.
    /// </summary>
    public Dictionary<string, List<string>> Errors { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the response ended with this post.
    /// </summary>
    public bool Ended { get; set; }
}

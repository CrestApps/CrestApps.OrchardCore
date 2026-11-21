namespace CrestApps.OrchardCore.Surveys.Core.Runtime;

/// <summary>
/// What a respondent sees when a response ends.
/// </summary>
public sealed class SurveyEndingView
{
    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the sanitized HTML message.
    /// </summary>
    public string MessageHtml { get; set; }

    /// <summary>
    /// Gets or sets a local or allowed URL to send the respondent to, or <see langword="null"/>.
    /// </summary>
    public string RedirectUrl { get; set; }

    /// <summary>
    /// Gets or sets whether the respondent was screened out.
    /// </summary>
    public bool ScreenedOut { get; set; }
}

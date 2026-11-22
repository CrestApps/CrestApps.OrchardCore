namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// Asks a signed-in visitor before a resume link resumes a response that does not belong to their account.
/// </summary>
public class SurveyResumeViewModel
{
    /// <summary>
    /// Gets or sets the response.
    /// </summary>
    public string ResponseId { get; set; }

    /// <summary>
    /// Gets or sets the resume token of the link, posted back with the confirmation.
    /// </summary>
    public string Token { get; set; }

    /// <summary>
    /// Gets or sets the survey title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the query string of the link without its token, kept on the confirmation post.
    /// </summary>
    public string QueryString { get; set; }
}

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The page a personal invitation link shows: it posts the start of the invited person's response (at once with
/// script, or with its button), so opening the link alone starts nothing.
/// </summary>
public class SurveyInvitationOpenViewModel
{
    /// <summary>
    /// Gets or sets the invitation token.
    /// </summary>
    public string Token { get; set; }

    /// <summary>
    /// Gets or sets the survey title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the page asks for a CAPTCHA (a bare link made for no one, on a survey that
    /// requires one), so it doesn't post at once.
    /// </summary>
    public bool NeedsCaptcha { get; set; }

    /// <summary>
    /// Gets or sets the message to show, or <see langword="null"/>.
    /// </summary>
    public string Message { get; set; }
}

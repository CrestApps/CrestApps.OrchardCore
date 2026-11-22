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
}

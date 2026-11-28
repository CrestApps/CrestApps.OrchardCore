namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The unsubscribe page of an invitation e-mail.
/// </summary>
public class SurveyInvitationUnsubscribeViewModel
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
    /// Gets or sets the address the invitation went to.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets whether the address is unsubscribed.
    /// </summary>
    public bool IsUnsubscribed { get; set; }
}

namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// Who may respond to a survey.
/// </summary>
public enum SurveyAudienceMode
{
    /// <summary>
    /// Anyone with the link.
    /// </summary>
    Anyone,

    /// <summary>
    /// Signed-in users with the Respond to surveys permission.
    /// </summary>
    SignedIn,

    /// <summary>
    /// Members of the chosen user audiences.
    /// </summary>
    Audiences,

    /// <summary>
    /// Only people who received an invitation.
    /// </summary>
    InvitationOnly,
}

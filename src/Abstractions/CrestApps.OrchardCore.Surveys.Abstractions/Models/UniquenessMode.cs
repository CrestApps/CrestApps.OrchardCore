namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// How a respondent is recognized for limits.
/// </summary>
public enum UniquenessMode
{
    /// <summary>
    /// By the signed-in user.
    /// </summary>
    SignedInUser,

    /// <summary>
    /// By the invitation.
    /// </summary>
    Invitation,

    /// <summary>
    /// By a browser cookie (weak: a respondent can clear it).
    /// </summary>
    Cookie,

    /// <summary>
    /// Not recognized; limits do not apply to respondents who are not signed in.
    /// </summary>
    None,
}

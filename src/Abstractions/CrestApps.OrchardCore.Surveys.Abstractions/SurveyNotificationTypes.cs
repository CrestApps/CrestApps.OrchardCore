namespace CrestApps.OrchardCore.Surveys;

/// <summary>
/// The notification types of Surveys.
/// </summary>
public static class SurveyNotificationTypes
{
    /// <summary>
    /// An invitation to respond.
    /// </summary>
    public const string Invitation = "SurveyInvitation";

    /// <summary>
    /// A reminder of an invitation.
    /// </summary>
    public const string Reminder = "SurveyReminder";

    /// <summary>
    /// A response of the user was graded.
    /// </summary>
    public const string ResponseGraded = "SurveyResponseGraded";

    /// <summary>
    /// A response needs a grader.
    /// </summary>
    public const string NeedsGrading = "SurveyNeedsGrading";
}

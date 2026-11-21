namespace CrestApps.OrchardCore.Surveys;

/// <summary>
/// The names of the survey templates Surveys seeds when it is turned on. Administrators may change or remove them;
/// code that starts from one falls back to a blank survey when it is gone.
/// </summary>
public static class SurveyTemplateNames
{
    /// <summary>
    /// An empty survey. It is built in and always offered.
    /// </summary>
    public const string Blank = "blank";

    /// <summary>
    /// Customer satisfaction.
    /// </summary>
    public const string CustomerSatisfaction = "customer-satisfaction";

    /// <summary>
    /// Net Promoter Score.
    /// </summary>
    public const string NetPromoterScore = "nps";

    /// <summary>
    /// Employee engagement.
    /// </summary>
    public const string EmployeeEngagement = "employee-engagement";

    /// <summary>
    /// Event feedback.
    /// </summary>
    public const string EventFeedback = "event-feedback";

    /// <summary>
    /// A scored quiz.
    /// </summary>
    public const string Quiz = "quiz";

    /// <summary>
    /// A one-question poll.
    /// </summary>
    public const string Poll = "poll";
}

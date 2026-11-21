namespace CrestApps.OrchardCore.Surveys;

/// <summary>
/// The webhook events of Surveys. Names and payloads never change once shipped.
/// </summary>
public static class SurveyWebhookEvents
{
    /// <summary>
    /// A survey version was published.
    /// </summary>
    public const string Published = "survey.published";

    /// <summary>
    /// A response was completed.
    /// </summary>
    public const string ResponseCompleted = "survey.response.completed";

    /// <summary>
    /// A response was graded.
    /// </summary>
    public const string ResponseGraded = "survey.response.graded";
}

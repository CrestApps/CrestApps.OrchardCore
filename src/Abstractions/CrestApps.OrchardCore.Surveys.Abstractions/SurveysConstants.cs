namespace CrestApps.OrchardCore.Surveys;

/// <summary>
/// The constants of Surveys.
/// </summary>
public static class SurveysConstants
{
    /// <summary>
    /// The YesSql collection that holds surveys, versions, banks, collectors and the other small records.
    /// </summary>
    public const string CollectionName = "Surveys";

    /// <summary>
    /// The YesSql collection that holds responses, kept apart because it is the large table.
    /// </summary>
    public const string ResponsesCollectionName = "SurveyResponses";

    /// <summary>
    /// The site settings group of Surveys.
    /// </summary>
    public const string SettingsGroupId = "surveys";

    /// <summary>
    /// The owner kind of files uploaded with a response, and the record kind of a response.
    /// </summary>
    public const string ResponseRecordKind = "SurveyResponse";

    /// <summary>
    /// The record kind of a survey.
    /// </summary>
    public const string SurveyRecordKind = "Survey";

    /// <summary>
    /// The prefix of every form input of a question: <c>q.{questionId}</c>.
    /// </summary>
    public const string AnswerPrefix = "q";

    /// <summary>
    /// The most rows read or written in one page by background work, exports and reports.
    /// </summary>
    public const int PageSize = 500;

    /// <summary>
    /// The feature ids of Surveys.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// Surveys: definitions, versions, the designer, controls, logic, responses, scoring and results.
        /// </summary>
        public const string Surveys = "CrestApps.OrchardCore.Surveys";

        /// <summary>
        /// Personal invitations, reminders and invitation notices.
        /// </summary>
        public const string Invitations = "CrestApps.OrchardCore.Surveys.Invitations";

        /// <summary>
        /// The Survey part, the Survey widget and the survey shortcode.
        /// </summary>
        public const string Embedding = "CrestApps.OrchardCore.Surveys.Embedding";

        /// <summary>
        /// QTI import into question banks.
        /// </summary>
        public const string Qti = "CrestApps.OrchardCore.Surveys.Qti";

        /// <summary>
        /// The headless JSON API.
        /// </summary>
        public const string Api = "CrestApps.OrchardCore.Surveys.Api";

        /// <summary>
        /// AI tools and summaries for surveys.
        /// </summary>
        public const string AI = "CrestApps.OrchardCore.Surveys.AI";
    }
}

namespace CrestApps.OrchardCore.Learning.Surveys;

/// <summary>
/// The names Learning surveys use.
/// </summary>
public static class SurveysConstants
{
    /// <summary>
    /// The feature.
    /// </summary>
    public const string Feature = "CrestApps.OrchardCore.Learning.Surveys";

    /// <summary>
    /// The course item kind of surveys, feedback surveys and choices alike.
    /// </summary>
    public const string SurveyKind = "Survey";

    /// <summary>
    /// The YesSql collection of responses.
    /// </summary>
    public const string CollectionName = "LearningSurveyResponses";

    /// <summary>
    /// The longest written answer a response keeps.
    /// </summary>
    public const int MaxTextAnswerLength = 4000;

    /// <summary>
    /// The most questions a survey may have.
    /// </summary>
    public const int MaxQuestions = 50;

    /// <summary>
    /// The most choices a question may have.
    /// </summary>
    public const int MaxOptions = 50;

    /// <summary>
    /// The lowest value of a rating question.
    /// </summary>
    public const int MinRating = 1;

    /// <summary>
    /// The highest value of a rating question.
    /// </summary>
    public const int MaxRating = 5;

    /// <summary>
    /// The content types the module creates.
    /// </summary>
    public static class ContentTypes
    {
        /// <summary>
        /// A feedback survey: several questions, usually with the totals kept to staff.
        /// </summary>
        public const string FeedbackSurvey = "FeedbackSurvey";

        /// <summary>
        /// A choice: one question whose totals learners may see, as a poll.
        /// </summary>
        public const string Choice = "Choice";
    }
}

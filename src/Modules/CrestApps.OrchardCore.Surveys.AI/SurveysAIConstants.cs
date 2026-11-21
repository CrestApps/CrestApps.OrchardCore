namespace CrestApps.OrchardCore.Surveys.AI;

/// <summary>
/// The constants of Surveys AI.
/// </summary>
public static class SurveysAIConstants
{
    /// <summary>
    /// The usage label of AI requests made by Surveys AI.
    /// </summary>
    public const string UsagePurpose = "Surveys";

    /// <summary>
    /// The most text answers a summary reads.
    /// </summary>
    public const int MaxSummarizedAnswers = 200;

    /// <summary>
    /// The names of the AI tools.
    /// </summary>
    public static class Tools
    {
        /// <summary>
        /// Lists the surveys the user manages or reads the results of.
        /// </summary>
        public const string ListSurveys = "list_surveys";

        /// <summary>
        /// Reads the results of a survey.
        /// </summary>
        public const string GetSurveyResults = "get_survey_results";

        /// <summary>
        /// Drafts a new survey from instructions.
        /// </summary>
        public const string CreateSurveyDraft = "create_survey_draft";
    }

    /// <summary>
    /// The ids of the prompt templates in <c>Templates/Prompts</c>.
    /// </summary>
    public static class Templates
    {
        /// <summary>
        /// Suggests a score and a comment for a written answer.
        /// </summary>
        public const string ScoreAnswer = "surveys-score-answer";

        /// <summary>
        /// Drafts a survey definition.
        /// </summary>
        public const string DraftSurvey = "surveys-draft-survey";

        /// <summary>
        /// Summarizes the answers to a written question.
        /// </summary>
        public const string SummarizeAnswers = "surveys-summarize-answers";

        /// <summary>
        /// Translates the texts of a survey.
        /// </summary>
        public const string Translate = "surveys-translate";
    }
}

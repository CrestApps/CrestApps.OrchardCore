namespace CrestApps.OrchardCore.Surveys.Recipes;

/// <summary>
/// The recipe step names of Surveys and the sections that hold their records.
/// </summary>
internal static class SurveysRecipeSteps
{
    /// <summary>
    /// Surveys with their versions and collectors.
    /// </summary>
    public const string Surveys = "Surveys";

    /// <summary>
    /// The name of the step that imports survey templates.
    /// </summary>
    public const string Templates = "SurveyTemplates";

    /// <summary>
    /// The section of the survey templates step that holds the templates.
    /// </summary>
    public const string TemplatesSection = "Templates";

    /// <summary>
    /// Question banks with their questions.
    /// </summary>
    public const string QuestionBanks = "SurveyQuestionBanks";

    /// <summary>
    /// Responses.
    /// </summary>
    public const string Responses = "SurveyResponses";

    /// <summary>
    /// Accommodations.
    /// </summary>
    public const string Overrides = "SurveyOverrides";

    /// <summary>
    /// Invitations.
    /// </summary>
    public const string Invitations = "SurveyInvitations";

    /// <summary>
    /// The section of the Surveys step.
    /// </summary>
    public const string SurveysSection = "Surveys";

    /// <summary>
    /// The section of the question banks step.
    /// </summary>
    public const string QuestionBanksSection = "QuestionBanks";

    /// <summary>
    /// The section of the responses step.
    /// </summary>
    public const string ResponsesSection = "Responses";

    /// <summary>
    /// The section of the accommodations step.
    /// </summary>
    public const string OverridesSection = "Overrides";

    /// <summary>
    /// The section of the invitations step.
    /// </summary>
    public const string InvitationsSection = "Invitations";
}

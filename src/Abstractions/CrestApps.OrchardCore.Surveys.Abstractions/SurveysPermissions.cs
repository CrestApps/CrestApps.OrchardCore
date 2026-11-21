using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Surveys;

/// <summary>
/// The permissions of Surveys. They live in the abstractions so consumers, such as Learning, check them without the
/// core library.
/// </summary>
public static class SurveysPermissions
{
    /// <summary>
    /// Manage every survey, its collectors, versions and settings.
    /// </summary>
    public static readonly Permission ManageSurveys = new("ManageSurveys", LocalizationSource.Create("Manage all surveys", typeof(SurveysPermissions)));

    /// <summary>
    /// Manage the surveys the user owns.
    /// </summary>
    public static readonly Permission ManageOwnSurveys = new("ManageOwnSurveys", LocalizationSource.Create("Manage own surveys", typeof(SurveysPermissions)), [ManageSurveys]);

    /// <summary>
    /// Add, change and remove the templates new surveys start from.
    /// </summary>
    public static readonly Permission ManageSurveyTemplates = new("ManageSurveyTemplates", LocalizationSource.Create("Manage survey templates", typeof(SurveysPermissions)), [ManageSurveys]);

    /// <summary>
    /// Publish, restore and close surveys.
    /// </summary>
    public static readonly Permission PublishSurveys = new("PublishSurveys", LocalizationSource.Create("Publish, restore and close surveys", typeof(SurveysPermissions)), [ManageSurveys]);

    /// <summary>
    /// See the responses and reports of every survey.
    /// </summary>
    public static readonly Permission ViewSurveyResponses = new("ViewSurveyResponses", LocalizationSource.Create("View responses of all surveys", typeof(SurveysPermissions)), [ManageSurveys]);

    /// <summary>
    /// See the responses and reports of the surveys the user owns.
    /// </summary>
    public static readonly Permission ViewOwnSurveyResponses = new("ViewOwnSurveyResponses", LocalizationSource.Create("View responses of own surveys", typeof(SurveysPermissions)), [ViewSurveyResponses, ManageOwnSurveys]);

    /// <summary>
    /// Export responses, read respondent identity in data sets and export responses in recipes.
    /// </summary>
    public static readonly Permission ExportSurveyResponses = new("ExportSurveyResponses", LocalizationSource.Create("Export survey responses with respondent identity", typeof(SurveysPermissions)), isSecurityCritical: true);

    /// <summary>
    /// Void and delete responses.
    /// </summary>
    public static readonly Permission DeleteSurveyResponses = new("DeleteSurveyResponses", LocalizationSource.Create("Void and delete survey responses", typeof(SurveysPermissions)), [ManageSurveys]);

    /// <summary>
    /// Grade responses that need a grader.
    /// </summary>
    public static readonly Permission GradeSurveyResponses = new("GradeSurveyResponses", LocalizationSource.Create("Grade survey responses", typeof(SurveysPermissions)), [ManageSurveys]);

    /// <summary>
    /// Grant and revoke accommodations.
    /// </summary>
    public static readonly Permission ManageSurveyOverrides = new("ManageSurveyOverrides", LocalizationSource.Create("Manage survey accommodations", typeof(SurveysPermissions)), [ManageSurveys]);

    /// <summary>
    /// Manage question banks and their questions.
    /// </summary>
    public static readonly Permission ManageQuestionBanks = new("ManageQuestionBanks", LocalizationSource.Create("Manage question banks", typeof(SurveysPermissions)));

    /// <summary>
    /// Read question banks, to pick questions in the designer.
    /// </summary>
    public static readonly Permission ViewQuestionBanks = new("ViewQuestionBanks", LocalizationSource.Create("View question banks", typeof(SurveysPermissions)), [ManageQuestionBanks]);

    /// <summary>
    /// Send and manage invitations.
    /// </summary>
    public static readonly Permission ManageSurveyInvitations = new("ManageSurveyInvitations", LocalizationSource.Create("Manage survey invitations", typeof(SurveysPermissions)), [ManageSurveys]);

    /// <summary>
    /// Respond to surveys open to signed-in users or to audiences.
    /// </summary>
    public static readonly Permission RespondToSurveys = new("RespondToSurveys", LocalizationSource.Create("Respond to surveys", typeof(SurveysPermissions)));

    /// <summary>
    /// Change the site settings of Surveys.
    /// </summary>
    public static readonly Permission ManageSurveySettings = new("ManageSurveySettings", LocalizationSource.Create("Manage survey settings", typeof(SurveysPermissions)));
}

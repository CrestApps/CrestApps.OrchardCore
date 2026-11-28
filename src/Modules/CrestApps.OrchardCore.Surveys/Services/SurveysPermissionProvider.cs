using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Declares the permissions of Surveys and who holds them by default.
/// </summary>
internal sealed class SurveysPermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        SurveysPermissions.ManageSurveys,
        SurveysPermissions.ManageOwnSurveys,
        SurveysPermissions.PublishSurveys,
        SurveysPermissions.PublishOwnSurveys,
        SurveysPermissions.ManageSurveyTemplates,
        SurveysPermissions.ViewSurveyResponses,
        SurveysPermissions.ViewOwnSurveyResponses,
        SurveysPermissions.ExportSurveyResponses,
        SurveysPermissions.DeleteSurveyResponses,
        SurveysPermissions.DeleteOwnSurveyResponses,
        SurveysPermissions.GradeSurveyResponses,
        SurveysPermissions.GradeOwnSurveyResponses,
        SurveysPermissions.ManageSurveyOverrides,
        SurveysPermissions.ManageOwnSurveyOverrides,
        SurveysPermissions.ManageQuestionBanks,
        SurveysPermissions.ViewQuestionBanks,
        SurveysPermissions.ManageSurveyInvitations,
        SurveysPermissions.ManageOwnSurveyInvitations,
        SurveysPermissions.RespondToSurveys,
        SurveysPermissions.ManageSurveySettings,
    ];

    /// <inheritdoc />
    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    /// <inheritdoc />
    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
        =>
        [
            new PermissionStereotype
            {
                Name = OrchardCoreConstants.Roles.Administrator,
                Permissions = _allPermissions,
            },
            new PermissionStereotype
            {
                Name = OrchardCoreConstants.Roles.Editor,

                // Editors work on their own surveys. Invitations send site e-mail, so ManageOwnSurveyInvitations is
                // left for an administrator to grant on purpose.
                Permissions =
                [
                    SurveysPermissions.ManageOwnSurveys,
                    SurveysPermissions.PublishOwnSurveys,
                    SurveysPermissions.ViewOwnSurveyResponses,
                    SurveysPermissions.DeleteOwnSurveyResponses,
                    SurveysPermissions.GradeOwnSurveyResponses,
                    SurveysPermissions.ManageOwnSurveyOverrides,
                    SurveysPermissions.ViewQuestionBanks,
                ],
            },
            new PermissionStereotype
            {
                Name = OrchardCoreConstants.Roles.Authenticated,
                Permissions =
                [
                    SurveysPermissions.RespondToSurveys,
                ],
            },
        ];
}

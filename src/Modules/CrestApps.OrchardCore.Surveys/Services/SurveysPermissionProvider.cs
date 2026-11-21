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
        SurveysPermissions.ViewSurveyResponses,
        SurveysPermissions.ViewOwnSurveyResponses,
        SurveysPermissions.ExportSurveyResponses,
        SurveysPermissions.DeleteSurveyResponses,
        SurveysPermissions.GradeSurveyResponses,
        SurveysPermissions.ManageSurveyOverrides,
        SurveysPermissions.ManageQuestionBanks,
        SurveysPermissions.ViewQuestionBanks,
        SurveysPermissions.ManageSurveyInvitations,
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
                Permissions =
                [
                    SurveysPermissions.ManageOwnSurveys,
                    SurveysPermissions.ViewOwnSurveyResponses,
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

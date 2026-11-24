using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Learning.Assessments;

internal sealed class AssessmentsPermissionsProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        AssessmentPermissions.ManageQuestionBanks,
        AssessmentPermissions.ViewQuestionBanks,
        AssessmentPermissions.GradeAssessments,
        AssessmentPermissions.OverrideGrades,
        AssessmentPermissions.ManageAssessmentOverrides,
        AssessmentPermissions.ManageGradeScales,
    ];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
        =>
        [
            new PermissionStereotype
            {
                Name = OrchardCoreConstants.Roles.Administrator,
                Permissions = _allPermissions,
            },
        ];
}

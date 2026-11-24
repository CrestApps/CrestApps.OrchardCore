using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Learning;

/// <summary>
/// Publishes the Learning permissions. Administrators get all of them and signed-in users can be learners; the
/// Learning roles recipe creates the suggested roles (Learning Administrator, Course Author, Instructor, Grader,
/// Training Manager and Learner).
/// </summary>
internal sealed class LearningPermissionsProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        LearningPermissions.ManageCourses,
        LearningPermissions.ManageOwnCourses,
        LearningPermissions.ManageEnrollments,
        LearningPermissions.ViewEnrollments,
        LearningPermissions.ViewLearnerRecords,
        LearningPermissions.ViewLearningReports,
        LearningPermissions.ExportLearnerRecords,
        LearningPermissions.AccessLearning,
    ];

    /// <inheritdoc />
    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    /// <inheritdoc />
    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
    {
        return
        [
            new PermissionStereotype
            {
                Name = OrchardCoreConstants.Roles.Administrator,
                Permissions = _allPermissions,
            },
            new PermissionStereotype
            {
                Name = OrchardCoreConstants.Roles.Authenticated,
                Permissions = [LearningPermissions.AccessLearning],
            },
        ];
    }
}

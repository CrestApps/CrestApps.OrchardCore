using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Learning.Classes;

/// <summary>
/// The permissions of Learning classes. The instructors and assistants on a course's staff also manage the sessions of
/// its classes when they can manage the courses they teach.
/// </summary>
public static class LearningClassesPermissions
{
    /// <summary>
    /// Add, change and cancel the sessions of every class, see their rosters and mark attendance.
    /// </summary>
    public static readonly Permission ManageLearningClasses = new(
        "ManageLearningClasses",
        LocalizationSource.Create("Manage learning classes", typeof(LearningClassesPermissions)),
        [LearningPermissions.ManageCourses]);
}

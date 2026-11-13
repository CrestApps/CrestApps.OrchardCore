using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Learning;

/// <summary>
/// The permissions of the Learning core. Callers check the broad permission with the record or content item as the
/// resource; an authorization handler narrows it to the course staff, the manager's reports and the learner's own records.
/// </summary>
public static class LearningPermissions
{
    /// <summary>
    /// Create and edit every course and course item.
    /// </summary>
    public static readonly Permission ManageCourses = new("ManageCourses", LocalizationSource.Create("Manage courses", typeof(LearningPermissions)));

    /// <summary>
    /// Create courses and edit the courses where the user is on the course staff.
    /// </summary>
    public static readonly Permission ManageOwnCourses = new("ManageOwnCourses", LocalizationSource.Create("Manage the courses you teach", typeof(LearningPermissions)), [ManageCourses]);

    /// <summary>
    /// Enroll, approve, suspend and withdraw learners.
    /// </summary>
    public static readonly Permission ManageEnrollments = new("ManageEnrollments", LocalizationSource.Create("Manage enrollments", typeof(LearningPermissions)));

    /// <summary>
    /// See who is enrolled.
    /// </summary>
    public static readonly Permission ViewEnrollments = new("ViewEnrollments", LocalizationSource.Create("View enrollments", typeof(LearningPermissions)), [ManageEnrollments]);

    /// <summary>
    /// See another person's progress, grades and attempts, within scope.
    /// </summary>
    public static readonly Permission ViewLearnerRecords = new("ViewLearnerRecords", LocalizationSource.Create("View learner records", typeof(LearningPermissions)), [ManageEnrollments]);

    /// <summary>
    /// Run the learning reports.
    /// </summary>
    public static readonly Permission ViewLearningReports = new("ViewLearningReports", LocalizationSource.Create("View learning reports", typeof(LearningPermissions)));

    /// <summary>
    /// Export learner records, which hold personal data.
    /// </summary>
    public static readonly Permission ExportLearnerRecords = new("ExportLearnerRecords", LocalizationSource.Create("Export learner records", typeof(LearningPermissions)));

    /// <summary>
    /// Be a learner: the catalog, one's own learning, transcript and certificates.
    /// </summary>
    public static readonly Permission AccessLearning = new("AccessLearning", LocalizationSource.Create("Access learning", typeof(LearningPermissions)));
}

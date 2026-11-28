using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Learning.Assessments;

/// <summary>
/// The permissions of Learning Assessments. Quizzes are surveys: course staff work on their course's quiz surveys through
/// their staff roles, and <see cref="GradeAssessments"/> and <see cref="OverrideGrades"/> reach the surveys of every
/// course item (and no other survey) through the Assessments access provider.
/// </summary>
public static class AssessmentPermissions
{
    /// <summary>
    /// Grade the work of every course (quizzes and coursework) on the Learning grading page.
    /// </summary>
    public static readonly Permission GradeAssessments = new("GradeAssessments", LocalizationSource.Create("Grade the work of every course", typeof(AssessmentPermissions)));

    /// <summary>
    /// Change grades that are already given.
    /// </summary>
    public static readonly Permission OverrideGrades = new("OverrideGrades", LocalizationSource.Create("Override grades", typeof(AssessmentPermissions)));

    /// <summary>
    /// Manage grade scales.
    /// </summary>
    public static readonly Permission ManageGradeScales = new("ManageGradeScales", LocalizationSource.Create("Manage grade scales", typeof(AssessmentPermissions)));
}

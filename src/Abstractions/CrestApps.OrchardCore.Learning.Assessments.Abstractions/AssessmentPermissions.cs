using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Learning.Assessments;

/// <summary>
/// The permissions of Learning assessments.
/// </summary>
public static class AssessmentPermissions
{
    /// <summary>
    /// Author questions and banks.
    /// </summary>
    public static readonly Permission ManageQuestionBanks = new("ManageQuestionBanks", LocalizationSource.Create("Manage question banks", typeof(AssessmentPermissions)));

    /// <summary>
    /// See questions and their answer keys.
    /// </summary>
    public static readonly Permission ViewQuestionBanks = new("ViewQuestionBanks", LocalizationSource.Create("View question banks and answer keys", typeof(AssessmentPermissions)), [ManageQuestionBanks]);

    /// <summary>
    /// Grade attempts that need a grader.
    /// </summary>
    public static readonly Permission GradeAssessments = new("GradeAssessments", LocalizationSource.Create("Grade assessments", typeof(AssessmentPermissions)));

    /// <summary>
    /// Change a score with a reason, regrade and void attempts.
    /// </summary>
    public static readonly Permission OverrideGrades = new("OverrideGrades", LocalizationSource.Create("Override grades", typeof(AssessmentPermissions)));

    /// <summary>
    /// Give a person more time, more attempts or another window.
    /// </summary>
    public static readonly Permission ManageAssessmentOverrides = new("ManageAssessmentOverrides", LocalizationSource.Create("Manage assessment overrides", typeof(AssessmentPermissions)));

    /// <summary>
    /// Manage grade scales.
    /// </summary>
    public static readonly Permission ManageGradeScales = new("ManageGradeScales", LocalizationSource.Create("Manage grade scales", typeof(AssessmentPermissions)));
}

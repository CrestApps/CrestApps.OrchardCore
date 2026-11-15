using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Learning.Programs.Core;

/// <summary>
/// The permissions of Learning programs.
/// </summary>
public static class LearningProgramsPermissions
{
    /// <summary>
    /// Create, change, run and delete required learning, and see who it covers.
    /// </summary>
    public static readonly Permission ManageRequiredLearning = new(
        "ManageRequiredLearning",
        LocalizationSource.Create("Manage required learning", typeof(LearningProgramsPermissions)));

    /// <summary>
    /// Exempt a person from required learning, such as for prior learning.
    /// </summary>
    public static readonly Permission ExemptFromRequiredLearning = new(
        "ExemptFromRequiredLearning",
        LocalizationSource.Create("Exempt people from required learning", typeof(LearningProgramsPermissions)),
        [ManageRequiredLearning]);

    /// <summary>
    /// See the required learning of the people who report to you.
    /// </summary>
    public static readonly Permission ViewTeamLearning = new(
        "ViewTeamLearning",
        LocalizationSource.Create("See the required learning of the people who report to you", typeof(LearningProgramsPermissions)),
        [ManageRequiredLearning]);
}

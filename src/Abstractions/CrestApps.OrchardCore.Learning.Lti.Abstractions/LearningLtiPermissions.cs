using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Learning.Lti;

/// <summary>
/// The permissions of External Tools.
/// </summary>
public static class LearningLtiPermissions
{
    /// <summary>
    /// Register, edit and remove external tools. Tools receive learners' data and post their grades, so this is a high
    /// trust permission of its own.
    /// </summary>
    public static readonly Permission ManageExternalTools = new(
        "ManageExternalTools",
        LocalizationSource.Create("Manage external tools", typeof(LearningLtiPermissions)),
        isSecurityCritical: true);
}

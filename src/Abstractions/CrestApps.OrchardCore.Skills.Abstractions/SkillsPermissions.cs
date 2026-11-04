using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Skills;

/// <summary>
/// The Skills permissions.
/// </summary>
public static class SkillsPermissions
{
    /// <summary>
    /// Add, change and remove skills and certifications in the catalog.
    /// </summary>
    public static readonly Permission ManageSkills = new("ManageSkills", LocalizationSource.Create("Manage skills and certifications", typeof(SkillsPermissions)));

    /// <summary>
    /// Give people skills and record their certifications.
    /// </summary>
    public static readonly Permission AssignSkills = new("AssignSkills", LocalizationSource.Create("Assign skills and record certifications", typeof(SkillsPermissions)), [ManageSkills]);

    /// <summary>
    /// See the skills and certifications people hold.
    /// </summary>
    public static readonly Permission ViewSkills = new("ViewSkills", LocalizationSource.Create("View skills and certifications", typeof(SkillsPermissions)), [AssignSkills]);
}

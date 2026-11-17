using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Banking.Checks;

/// <summary>
/// The Checks permissions.
/// </summary>
public static class ChecksPermissions
{
    /// <summary>
    /// Add and change check layouts, their signature images and the check line font.
    /// </summary>
    public static readonly Permission ManageCheckLayouts = new("ManageCheckLayouts", LocalizationSource.Create("Manage check layouts", typeof(ChecksPermissions)), isSecurityCritical: true);

    /// <summary>
    /// Issue, print, void, reissue and stop checks, record cleared checks, and set the next check number.
    /// </summary>
    public static readonly Permission ManageChecks = new("ManageChecks", LocalizationSource.Create("Issue and print checks", typeof(ChecksPermissions)), isSecurityCritical: true);

    /// <summary>
    /// See the check register.
    /// </summary>
    public static readonly Permission ViewCheckRegister = new("ViewCheckRegister", LocalizationSource.Create("View the check register", typeof(ChecksPermissions)), [ManageChecks]);
}

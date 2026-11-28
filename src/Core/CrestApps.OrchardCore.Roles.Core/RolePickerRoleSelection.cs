using CrestApps.OrchardCore.Roles.Core.Models;

namespace CrestApps.OrchardCore.Roles.Core;

/// <summary>
/// The rules of the roles picked in a Role Picker part, from its settings: excluded roles are dropped, and the Required
/// and Allow select multiple settings are checked. The part's editor and the AI tools apply the same rules; each words
/// its own messages.
/// </summary>
public static class RolePickerRoleSelection
{
    /// <summary>
    /// Returns the picked roles without the roles the settings exclude, in the order given.
    /// </summary>
    /// <param name="settings">The part's settings.</param>
    /// <param name="roles">The picked roles.</param>
    /// <returns>The roles that are kept.</returns>
    public static string[] Select(RolePickerPartSettings settings, IEnumerable<string> roles)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return (roles ?? []).Except(settings.ExcludedRoles ?? []).ToArray();
    }

    /// <summary>
    /// Checks the kept roles against the Required and Allow select multiple settings.
    /// </summary>
    /// <param name="settings">The part's settings.</param>
    /// <param name="selectedRoles">The kept roles, from <see cref="Select"/>.</param>
    /// <returns>The problem, or <see cref="RolePickerSelectionProblem.None"/>.</returns>
    public static RolePickerSelectionProblem Check(RolePickerPartSettings settings, IReadOnlyCollection<string> selectedRoles)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(selectedRoles);

        if (settings.Required && selectedRoles.Count == 0)
        {
            return settings.AllowSelectMultiple
                ? RolePickerSelectionProblem.AtLeastOneRoleRequired
                : RolePickerSelectionProblem.RoleRequired;
        }

        if (!settings.AllowSelectMultiple && selectedRoles.Count > 1)
        {
            return RolePickerSelectionProblem.OnlyOneRoleAllowed;
        }

        return RolePickerSelectionProblem.None;
    }
}

/// <summary>
/// What is wrong with the roles picked in a Role Picker part.
/// </summary>
public enum RolePickerSelectionProblem
{
    /// <summary>
    /// Nothing.
    /// </summary>
    None,

    /// <summary>
    /// The part requires a role and allows one.
    /// </summary>
    RoleRequired,

    /// <summary>
    /// The part requires at least one role and allows several.
    /// </summary>
    AtLeastOneRoleRequired,

    /// <summary>
    /// The part allows only one role.
    /// </summary>
    OnlyOneRoleAllowed,
}

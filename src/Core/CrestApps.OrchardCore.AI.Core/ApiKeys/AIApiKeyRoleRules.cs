using OrchardCore;

namespace CrestApps.OrchardCore.AI.Core.ApiKeys;

/// <summary>
/// Which roles an editor may give an AI API key. A key acts with its roles' permissions, so giving it a role is
/// granting that role: an editor may only give the roles they hold themselves, unless they may manage roles, and only
/// roles that exist. Roles the key already had stay, so an editor can change other settings of a key a more powerful
/// user created without being refused.
/// </summary>
public static class AIApiKeyRoleRules
{
    /// <summary>
    /// Returns the posted roles the editor may not give the key.
    /// </summary>
    /// <param name="postedRoles">The roles posted for the key.</param>
    /// <param name="existingRoles">The roles the key had before the change.</param>
    /// <param name="availableRoles">The roles that exist on the site.</param>
    /// <param name="editorHoldsRole">Whether the editor holds a role.</param>
    /// <param name="editorCanManageRoles">Whether the editor may manage roles, and so give any role.</param>
    /// <returns>The roles that are refused; empty when every posted role is allowed.</returns>
    public static IReadOnlyList<string> GetRefusedRoles(
        IEnumerable<string> postedRoles,
        IEnumerable<string> existingRoles,
        IEnumerable<string> availableRoles,
        Func<string, bool> editorHoldsRole,
        bool editorCanManageRoles)
    {
        ArgumentNullException.ThrowIfNull(editorHoldsRole);

        var existing = new HashSet<string>(existingRoles ?? [], StringComparer.OrdinalIgnoreCase);
        var available = new HashSet<string>(availableRoles ?? [], StringComparer.OrdinalIgnoreCase);
        var refused = new List<string>();

        foreach (var role in (postedRoles ?? []).Where(role => !string.IsNullOrWhiteSpace(role)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.Equals(role, OrchardCoreConstants.Roles.Anonymous, StringComparison.OrdinalIgnoreCase) || !available.Contains(role))
            {
                refused.Add(role);

                continue;
            }

            if (existing.Contains(role) || editorCanManageRoles || editorHoldsRole(role))
            {
                continue;
            }

            refused.Add(role);
        }

        return refused;
    }
}

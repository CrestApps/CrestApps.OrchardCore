using System.Security.Claims;
using OrchardCore;

namespace CrestApps.OrchardCore.Roles.Core;

/// <summary>
/// The roles a reader holds for content restricted to roles. Orchard Core never puts the Authenticated or Anonymous role
/// in a user's claims, so they are added here: Authenticated stands for anyone signed in, Anonymous for a visitor.
/// </summary>
public static class ContentAccessRoles
{
    /// <summary>
    /// The roles of someone who reads content: <c>Anonymous</c> for a visitor; <c>Authenticated</c> and the roles in
    /// their claims for someone signed in.
    /// </summary>
    /// <param name="user">The reader, or <see langword="null"/> for a visitor.</param>
    /// <returns>The role names.</returns>
    public static IReadOnlyList<string> GetReaderRoles(ClaimsPrincipal user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return [OrchardCoreConstants.Roles.Anonymous];
        }

        var roles = new List<string> { OrchardCoreConstants.Roles.Authenticated };

        foreach (var identity in user.Identities)
        {
            foreach (var claim in identity.FindAll(identity.RoleClaimType))
            {
                if (!string.IsNullOrEmpty(claim.Value) && !roles.Contains(claim.Value, StringComparer.OrdinalIgnoreCase))
                {
                    roles.Add(claim.Value);
                }
            }
        }

        return roles;
    }

    /// <summary>
    /// Whether a reader holds any of the given roles, <c>Authenticated</c> and <c>Anonymous</c> included.
    /// </summary>
    /// <param name="user">The reader, or <see langword="null"/> for a visitor.</param>
    /// <param name="roleNames">The roles.</param>
    /// <returns>Whether they hold one.</returns>
    public static bool IsInAnyRole(ClaimsPrincipal user, IEnumerable<string> roleNames)
    {
        ArgumentNullException.ThrowIfNull(roleNames);

        var held = GetReaderRoles(user);

        return roleNames.Any(role => held.Contains(role, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Whether a reader sees every item whatever its roles: people in the Administrator role do.
    /// </summary>
    /// <param name="user">The reader.</param>
    /// <returns>Whether they see everything.</returns>
    public static bool SeesEverything(ClaimsPrincipal user)
        => user?.IsInRole(OrchardCoreConstants.Roles.Administrator) == true;
}

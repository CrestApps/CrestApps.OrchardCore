using CrestApps.OrchardCore.Memberships.Models;
using CrestApps.OrchardCore.Users;

namespace CrestApps.OrchardCore.Memberships.Services;

/// <summary>
/// Reports the roles a user's active memberships granted before grants were recorded by source. The roles a
/// membership put its member in are its <see cref="Membership.AddedRoleNames"/>; the others the member was in before.
/// </summary>
public sealed class MembershipRoleGrantSource : IRoleGrantSource
{
    /// <summary>
    /// The source name that memberships' role grants are recorded under.
    /// </summary>
    public const string SourceName = "Membership";

    private readonly IMembershipStore _memberships;

    /// <summary>
    /// Initializes a new instance of the <see cref="MembershipRoleGrantSource"/> class.
    /// </summary>
    /// <param name="memberships">The store of memberships.</param>
    public MembershipRoleGrantSource(IMembershipStore memberships)
    {
        _memberships = memberships;
    }

    /// <inheritdoc/>
    public string Source => SourceName;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ExistingRoleGrant>> ListExistingGrantsAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return [];
        }

        return [.. (await _memberships.ListByUserAsync(userId))
            .Where(membership => membership.Status == MembershipStatus.Active)
            .SelectMany(membership => (membership.RoleNames ?? []).Select(role => new ExistingRoleGrant
            {
                RoleName = role,
                SourceId = membership.ItemId,
                RoleAddedBySource = membership.AddedRoleNames?.Contains(role, StringComparer.OrdinalIgnoreCase) == true,
            }))];
    }
}

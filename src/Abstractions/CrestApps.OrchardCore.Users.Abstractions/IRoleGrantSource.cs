namespace CrestApps.OrchardCore.Users;

/// <summary>
/// A kind of thing that grants roles through <see cref="IRoleGrantService"/>, such as memberships or subscriptions.
/// The first time the service touches a user, it asks every source for the grants that already exist, so grants made
/// before they were recorded by source are kept.
/// </summary>
public interface IRoleGrantSource
{
    /// <summary>
    /// Gets the source name that this source's grants are recorded under.
    /// </summary>
    string Source { get; }

    /// <summary>
    /// Lists the roles that this source grants to a user now, such as through the user's active memberships.
    /// </summary>
    /// <param name="userId">The unique ID of the user.</param>
    Task<IReadOnlyList<ExistingRoleGrant>> ListExistingGrantsAsync(string userId);
}

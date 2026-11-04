namespace CrestApps.OrchardCore.Users;

/// <summary>
/// Finds users for user pickers and other lookups, so every module searches users the same way. The service does not
/// authorize: an endpoint that exposes it checks <see cref="UserSearchPermissions.SearchUsers"/>, or its own permission,
/// first.
/// </summary>
public interface IUserSearchService
{
    /// <summary>
    /// Finds enabled users that match the query, ordered by user name.
    /// </summary>
    /// <param name="query">What to look for.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The users found, at most <see cref="UserSearchQuery.MaxResults"/>.</returns>
    Task<IReadOnlyList<UserSearchResult>> SearchAsync(UserSearchQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the users with the given ids, enabled or not.
    /// </summary>
    /// <param name="userIds">The user ids.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The users found; an id that matches no user is left out.</returns>
    Task<IReadOnlyList<UserSearchResult>> GetAsync(IEnumerable<string> userIds, CancellationToken cancellationToken = default);
}

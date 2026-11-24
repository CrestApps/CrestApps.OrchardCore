namespace CrestApps.OrchardCore.Users;

/// <summary>
/// Defines a named group of users, such as the people who work for the business, so user pickers and searches can be
/// limited to it with <see cref="UserSearchQuery.Audience"/>. Users never names the modules that define audiences.
/// </summary>
public interface IUserAudienceProvider
{
    /// <summary>
    /// Gets the audience name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the roles whose members make up the audience.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The role names; empty when the audience has no members; <see langword="null"/> when the audience isn't
    /// limited right now (for example a setting turns it off), so every user can be found.</returns>
    Task<IReadOnlyCollection<string>> GetRoleNamesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the users, out of some found by a search, that are left out of the audience, such as people who are
    /// already linked to a record so a picker offers only the ones still free. Only the found users are passed, so
    /// the work stays small however many users the site has.
    /// </summary>
    /// <param name="userIds">The ids of the users a search found.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The ids of the users to leave out; empty to keep them all.</returns>
    Task<IReadOnlyCollection<string>> GetExcludedUserIdsAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<string>>([]);
}

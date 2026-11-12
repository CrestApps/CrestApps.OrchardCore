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
    /// <returns>The role names; empty when the audience has no members.</returns>
    Task<IReadOnlyCollection<string>> GetRoleNamesAsync(CancellationToken cancellationToken = default);
}
